using System.Net.Http.Headers;
using System.Text.Json;
using pentyflixApi.Models.Reddit;
using Microsoft.Extensions.Caching.Memory;

namespace pentyflixApi.Services;

public class RedditService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RedditService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public RedditService(HttpClient httpClient, ILogger<RedditService> logger, IConfiguration configuration, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _cache = cache;
        
        // Set up the HttpClient for Reddit API
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "PentyFlix/1.0");
    }

    public async Task<List<RedditMediaPost>> GetMediaPostsFromSubreddit(string subredditName, int limit = 25, string timeFrame = "week")
    {
        // Create a cache key
        string cacheKey = $"reddit_{subredditName}_{limit}_{timeFrame}";
        
        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out List<RedditMediaPost> cachedPosts))
        {
            _logger.LogInformation($"Returning cached media posts for r/{subredditName}");
            return cachedPosts;
        }
        
        // If not in cache, fetch from API
        var posts = await FetchMediaPostsFromSubreddit(subredditName, limit, timeFrame);
        
        // Cache the results for 15 minutes
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));
        
        _cache.Set(cacheKey, posts, cacheOptions);
        
        return posts;
    }

    private async Task<List<RedditMediaPost>> FetchMediaPostsFromSubreddit(string subredditName, int limit = 25, string timeFrame = "week")
    {
        try
        {
            // Ensure subreddit name is formatted correctly
            subredditName = subredditName.Trim().ToLower();
            if (subredditName.StartsWith("r/"))
            {
                subredditName = subredditName.Substring(2);
            }
            
            _logger.LogInformation($"Fetching media posts from r/{subredditName}");
            
            // Build the Reddit API URL
            string url = $"https://www.reddit.com/r/{subredditName}/top.json?limit={limit}&t={timeFrame}";
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            string content = await response.Content.ReadAsStringAsync();
            var redditResponse = JsonSerializer.Deserialize<RedditResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (redditResponse == null || redditResponse.Data == null || redditResponse.Data.Children == null)
            {
                _logger.LogWarning($"No data found for subreddit: {subredditName}");
                return new List<RedditMediaPost>();
            }

            // Extract media posts
            var mediaPosts = new List<RedditMediaPost>();
            
            foreach (var post in redditResponse.Data.Children)
            {
                // Only include posts with media (images or videos)
                if (post?.Data == null) continue;
                
                var mediaUrl = ExtractMediaUrl(post.Data);
                
                if (!string.IsNullOrEmpty(mediaUrl))
                {
                    mediaPosts.Add(new RedditMediaPost
                    {
                        Title = post.Data.Title,
                        Author = post.Data.Author,
                        Permalink = $"https://www.reddit.com{post.Data.Permalink}",
                        Url = mediaUrl,
                        Thumbnail = post.Data.Thumbnail,
                        Score = post.Data.Score,
                        CreatedUtc = DateTimeOffset.FromUnixTimeSeconds((long)post.Data.CreatedUtc).LocalDateTime,
                        IsVideo = post.Data.IsVideo,
                        MediaType = DetermineMediaType(mediaUrl, post.Data.IsVideo)
                    });
                }
            }
            
            _logger.LogInformation($"Found {mediaPosts.Count} media posts in r/{subredditName}");
            return mediaPosts;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"Error fetching data from Reddit for subreddit: {subredditName}");
            throw new Exception($"Error fetching media from r/{subredditName}: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, $"Error parsing Reddit JSON response for subreddit: {subredditName}");
            throw new Exception($"Error processing Reddit data: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error when fetching from Reddit for subreddit: {subredditName}");
            throw;
        }
    }
    
    private string ExtractMediaUrl(RedditPostData postData)
    {
        // Handle different types of media
        
        // Direct image links (most common)
        if (!string.IsNullOrEmpty(postData.Url) &&
            (postData.Url.EndsWith(".jpg") || postData.Url.EndsWith(".jpeg") || 
            postData.Url.EndsWith(".png") || postData.Url.EndsWith(".gif")))
        {
            return postData.Url;
        }
        
        // Reddit-hosted images
        if (postData.Url?.Contains("i.redd.it") == true)
        {
            return postData.Url;
        }
        
        // Reddit-hosted videos
        if (postData.IsVideo && postData.Media?.RedditVideo?.FallbackUrl != null)
        {
            return postData.Media.RedditVideo.FallbackUrl;
        }
        
        // Imgur links without extensions
        if (postData.Url?.Contains("imgur.com") == true && !postData.Url.EndsWith(".jpg") && !postData.Url.EndsWith(".png"))
        {
            // Convert imgur links to direct image links
            if (postData.Url.Contains("imgur.com/a/") || postData.Url.Contains("imgur.com/gallery/"))
            {
                // This is an album, can't easily get a direct image URL
                return "";
            }
            
            var imgurId = postData.Url.Split('/').Last();
            return $"https://i.imgur.com/{imgurId}.jpg";
        }
        
        // Gallery posts
        if (postData.IsGallery && postData.GalleryData?.Items?.Count > 0)
        {
            var firstItem = postData.GalleryData.Items.First();
            if (postData.MediaMetadata?.TryGetValue(firstItem.MediaId, out var mediaMetadata) == true)
            {
                return $"https://i.redd.it/{firstItem.MediaId}.{mediaMetadata.m}";
            }
        }
        
        // No media found
        return "";
    }
    
    private string DetermineMediaType(string url, bool isVideo)
    {
        if (isVideo)
        {
            return "video";
        }
        
        if (string.IsNullOrEmpty(url))
        {
            return "unknown";
        }
        
        var extension = url.Split('.').Last().ToLower();
        
        switch (extension)
        {
            case "jpg":
            case "jpeg":
            case "png":
                return "image";
            case "gif":
                return "gif";
            default:
                return "unknown";
        }
    }
}