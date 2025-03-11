using System.Text.Json.Serialization;

namespace pentyflixApi.Models.Reddit;

// Model returned to clients
public class RedditCategory
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Url { get; set; }
    public int? SubscriberCount { get; set; }  // Make nullable
    public bool IsNsfw { get; set; }
    public string IconUrl { get; set; }
    public string BannerUrl { get; set; }
    public DateTime CreatedUtc { get; set; }
}

// Internal models for deserializing Reddit API responses
public class RedditCategoryResponse
{
    public RedditCategoryResponseData Data { get; set; }
}

public class RedditCategoryResponseData
{
    public List<RedditCategoryItem> Children { get; set; }
    public string After { get; set; }
    public string Before { get; set; }
}

public class RedditCategoryItem
{
    public RedditCategoryItemData Data { get; set; }
}

public class RedditCategoryItemData
{
    public string Id { get; set; }
    public string Name { get; set; }
    
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }
    
    public string Title { get; set; }
    
    [JsonPropertyName("public_description")]
    public string Description { get; set; }
    
    public string Url { get; set; }
    
    [JsonPropertyName("subscribers")]
    public int? SubscriberCount { get; set; }  // Make nullable
    
    [JsonPropertyName("over18")]
    public bool IsNsfw { get; set; }
    
    [JsonPropertyName("icon_img")]
    public string IconUrl { get; set; }
    
    [JsonPropertyName("banner_img")]
    public string BannerUrl { get; set; }
    
    [JsonPropertyName("created_utc")]
    public double CreatedUtcRaw { get; set; }
    
    [JsonIgnore]
    public DateTime CreatedUtc => DateTimeOffset.FromUnixTimeSeconds((long)CreatedUtcRaw).DateTime;
}