using System.ComponentModel;
using System.Text.Json.Serialization;

namespace pentyflixApi.Models.Reddit;

// This is the model we return to clients
public class RedditMediaPost
{
    public string Title { get; set; }
    public string Author { get; set; }
    public string Permalink { get; set; }
    public string Url { get; set; }
    public string Thumbnail { get; set; }
    public int Score { get; set; }
    [JsonPropertyName("created_utc")]
    public DateTime CreatedUtc { get; set; }
    public bool IsVideo { get; set; }
    public string MediaType { get; set; } // "image", "video", "gif", etc.
}

// Internal models for deserializing Reddit API responses
public class RedditResponse
{
    public RedditResponseData Data { get; set; }
}

public class RedditResponseData
{
    public List<RedditPost> Children { get; set; }
}

public class RedditPost
{
    public RedditPostData Data { get; set; }
}

public class RedditPostData
{
    public string Title { get; set; }
    public string Author { get; set; }
    public string Permalink { get; set; }
    public string Url { get; set; }
    public string Thumbnail { get; set; }
    public int Score { get; set; }
    [JsonPropertyName("created_utc")]
    public double CreatedUtc { get; set; }
    public bool IsVideo { get; set; }
    
    [JsonPropertyName("is_gallery")]
    public bool IsGallery { get; set; }
    
    [JsonPropertyName("gallery_data")]
    public GalleryData GalleryData { get; set; }
    
    [JsonPropertyName("media_metadata")]
    public Dictionary<string, MediaMetadata> MediaMetadata { get; set; }
    
    public RedditMedia Media { get; set; }
}

public class RedditMedia
{
    [JsonPropertyName("reddit_video")]
    public RedditVideo RedditVideo { get; set; }
}

public class RedditVideo
{
    [JsonPropertyName("fallback_url")]
    public string FallbackUrl { get; set; }
}

public class GalleryData
{
    public List<GalleryItem> Items { get; set; }
}

public class GalleryItem
{
    [JsonPropertyName("media_id")]
    public string MediaId { get; set; }
}

public class MediaMetadata
{
    public string m { get; set; } // Media extension (jpg, png)
}