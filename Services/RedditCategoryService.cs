using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using pentyflixApi.Models.Reddit;

namespace pentyflixApi.Services;

public class RedditCategoryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RedditCategoryService> _logger;
    private readonly IMemoryCache _cache;

    public RedditCategoryService(HttpClient httpClient, ILogger<RedditCategoryService> logger, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;

        // Set up the HttpClient for Reddit API
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "PentyFlix/1.0");
    }

    public async Task<List<RedditCategory>> GetPopularCategories(int limit = 25)
    {
        string cacheKey = $"reddit_categories_popular_{limit}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out List<RedditCategory> cachedCategories))
        {
            _logger.LogInformation("Returning cached popular categories");
            return cachedCategories;
        }

        var categories = await FetchPopularCategories(limit);

        // Cache for 1 hour (popular categories don't change often)
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromHours(1));

        _cache.Set(cacheKey, categories, cacheOptions);

        return categories;
    }

    public async Task<List<RedditCategory>> SearchCategories(string query, int limit = 25)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<RedditCategory>();
        }

        // Don't cache search results as they're specific to each query
        return await FetchCategoriesBySearch(query, limit);
    }

    public async Task<RedditCategory> GetCategoryDetails(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            throw new ArgumentException("Category name is required", nameof(categoryName));
        }

        // Clean up the category name
        categoryName = categoryName.Trim().ToLower();
        if (categoryName.StartsWith("r/"))
        {
            categoryName = categoryName.Substring(2);
        }

        string cacheKey = $"reddit_category_details_{categoryName}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out RedditCategory cachedCategory))
        {
            _logger.LogInformation($"Returning cached details for r/{categoryName}");
            return cachedCategory;
        }

        var categoryDetails = await FetchCategoryDetails(categoryName);

        // Cache details for 30 minutes
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

        _cache.Set(cacheKey, categoryDetails, cacheOptions);

        return categoryDetails;
    }

    private async Task<List<RedditCategory>> FetchPopularCategories(int limit)
    {
        try
        {
            _logger.LogInformation($"Fetching popular categories, limit: {limit}");

            // Build the Reddit API URL for popular subreddits
            string url = $"https://www.reddit.com/subreddits/popular.json?limit={limit}";

            return await FetchCategoriesFromUrl(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching popular categories");
            throw new Exception("Failed to retrieve popular categories", ex);
        }
    }

    private async Task<List<RedditCategory>> FetchCategoriesBySearch(string query, int limit)
    {
        try
        {
            _logger.LogInformation($"Searching categories with query: '{query}', limit: {limit}");

            // Build the Reddit API URL for subreddit search
            string encodedQuery = Uri.EscapeDataString(query);
            string url = $"https://www.reddit.com/subreddits/search.json?q={encodedQuery}&limit={limit}";

            return await FetchCategoriesFromUrl(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error searching for categories with query: '{query}'");
            throw new Exception($"Failed to search for categories with query: '{query}'", ex);
        }
    }

    private async Task<RedditCategory> FetchCategoryDetails(string categoryName)
    {
        try
        {
            _logger.LogInformation($"Fetching details for category r/{categoryName}");

            // Build the Reddit API URL for subreddit info
            string url = $"https://www.reddit.com/r/{categoryName}/about.json";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();

            // Reddit returns different format for individual subreddit info
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var aboutResponse = JsonDocument.Parse(content);

            // Navigate to the data object
            var data = aboutResponse.RootElement.GetProperty("data");

            // Map to our model, with safe property access
            var category = new RedditCategory
            {
                Name = data.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : string.Empty,
                DisplayName = data.TryGetProperty("display_name", out var displayEl) ? displayEl.GetString() : string.Empty,
                Title = data.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : string.Empty,
                Description = data.TryGetProperty("public_description", out var descEl) ? descEl.GetString() : string.Empty,
                Url = data.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : string.Empty,
                IsNsfw = data.TryGetProperty("over18", out var nsfwEl) && nsfwEl.GetBoolean(),
                IconUrl = data.TryGetProperty("icon_img", out var iconEl) ? iconEl.GetString() : string.Empty,
                BannerUrl = data.TryGetProperty("banner_img", out var bannerEl) ? bannerEl.GetString() : string.Empty,
            };

            // Handle subscribers which can be null
            if (data.TryGetProperty("subscribers", out var subsEl) && subsEl.ValueKind != JsonValueKind.Null)
            {
                try { category.SubscriberCount = subsEl.GetInt32(); }
                catch { category.SubscriberCount = 0; }
            }
            else
            {
                category.SubscriberCount = 0;
            }

            // Handle created_utc which can sometimes be problematic
            if (data.TryGetProperty("created_utc", out var createdEl) && createdEl.ValueKind != JsonValueKind.Null)
            {
                try
                {
                    double timestamp = createdEl.GetDouble();
                    category.CreatedUtc = DateTimeOffset.FromUnixTimeSeconds((long)timestamp).DateTime;
                }
                catch
                {
                    category.CreatedUtc = DateTime.UtcNow;
                }
            }
            else
            {
                category.CreatedUtc = DateTime.UtcNow;
            }

            return category;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"Error fetching details for category r/{categoryName}");
            throw new Exception($"Failed to retrieve details for r/{categoryName}", ex);
        }
    }

    private async Task<List<RedditCategory>> FetchCategoriesFromUrl(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();

            // Use option to ignore null values and handle case-insensitive properties
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var redditResponse = JsonSerializer.Deserialize<RedditCategoryResponse>(content, options);

            if (redditResponse?.Data?.Children == null)
            {
                _logger.LogWarning("No categories found in response");
                return new List<RedditCategory>();
            }

            var categories = new List<RedditCategory>();

            foreach (var item in redditResponse.Data.Children)
            {
                if (item?.Data == null) continue;

                // Use null-conditional and null-coalescing operators to handle potential nulls
                DateTime createdDate;
                try
                {
                    createdDate = DateTimeOffset.FromUnixTimeSeconds((long)item.Data.CreatedUtcRaw).DateTime;
                }
                catch
                {
                    createdDate = DateTime.UtcNow;
                }

                categories.Add(new RedditCategory
                {
                    Name = item.Data.Name ?? string.Empty,
                    DisplayName = item.Data.DisplayName ?? string.Empty,
                    Title = item.Data.Title ?? string.Empty,
                    Description = item.Data.Description ?? string.Empty,
                    Url = item.Data.Url ?? string.Empty,
                    SubscriberCount = item.Data.SubscriberCount ?? 0,
                    IsNsfw = item.Data.IsNsfw,
                    IconUrl = item.Data.IconUrl ?? string.Empty,
                    BannerUrl = item.Data.BannerUrl ?? string.Empty,
                    CreatedUtc = createdDate
                });
            }

            return categories;
        }
        catch (JsonException ex)
        {
            // Try an alternative approach using JsonDocument for more flexibility
            _logger.LogWarning(ex, "Error deserializing response as RedditCategoryResponse, trying JsonDocument approach");
            return await FetchCategoriesFromUrlUsingJsonDocument(url);
        }
    }
    private async Task<List<RedditCategory>> FetchCategoriesFromUrlUsingJsonDocument(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync();

        // Parse as JsonDocument for more dynamic handling
        using (JsonDocument document = JsonDocument.Parse(content))
        {
            var categories = new List<RedditCategory>();

            // Navigate to children array
            if (document.RootElement.TryGetProperty("data", out var dataElement) &&
                dataElement.TryGetProperty("children", out var childrenElement) &&
                childrenElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in childrenElement.EnumerateArray())
                {
                    if (item.TryGetProperty("data", out var itemData))
                    {
                        string name = itemData.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : string.Empty;
                        string displayName = itemData.TryGetProperty("display_name", out var displayNameElement) ? displayNameElement.GetString() : string.Empty;
                        string title = itemData.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : string.Empty;
                        string description = itemData.TryGetProperty("public_description", out var descElement) ? descElement.GetString() : string.Empty;
                        string subredditUrl = itemData.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : string.Empty;

                        int subscribers = 0;
                        if (itemData.TryGetProperty("subscribers", out var subsElement) &&
                            subsElement.ValueKind != JsonValueKind.Null)
                        {
                            try { subscribers = subsElement.GetInt32(); } catch { /* ignore parsing errors */ }
                        }

                        // Fixed: Check for Null before accessing GetBoolean
                        bool isNsfw = false;
                        if (itemData.TryGetProperty("over18", out var nsfwElement) &&
                            nsfwElement.ValueKind != JsonValueKind.Null)
                        {
                            try { isNsfw = nsfwElement.GetBoolean(); } catch { /* ignore parsing errors */ }
                        }

                        string iconUrl = itemData.TryGetProperty("icon_img", out var iconElement) ? iconElement.GetString() : string.Empty;
                        string bannerUrl = itemData.TryGetProperty("banner_img", out var bannerElement) ? bannerElement.GetString() : string.Empty;

                        DateTime createdDate = DateTime.UtcNow;
                        if (itemData.TryGetProperty("created_utc", out var createdElement) &&
                            createdElement.ValueKind != JsonValueKind.Null)
                        {
                            try
                            {
                                double timestamp = createdElement.GetDouble();
                                createdDate = DateTimeOffset.FromUnixTimeSeconds((long)timestamp).DateTime;
                            }
                            catch { /* Use default date */ }
                        }

                        categories.Add(new RedditCategory
                        {
                            Name = name,
                            DisplayName = displayName,
                            Title = title,
                            Description = description,
                            Url = subredditUrl, // Note: This should be 'url' (lowercase) not 'Url'
                            SubscriberCount = subscribers,
                            IsNsfw = isNsfw,
                            IconUrl = iconUrl,
                            BannerUrl = bannerUrl,
                            CreatedUtc = createdDate
                        });
                    }
                }
            }

            return categories;
        }
    }
}