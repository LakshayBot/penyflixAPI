using Microsoft.AspNetCore.Mvc;
using pentyflixApi.Models.Reddit;
using pentyflixApi.Services;

namespace pentyflixApi.Controllers.Reddit
{
    [ApiController]
    [Route("api/[controller]")]
    public class RedditController : ControllerBase
    {
        private readonly RedditService _redditService;
        private readonly ILogger<RedditController> _logger;

        public RedditController(RedditService redditService, ILogger<RedditController> logger)
        {
            _redditService = redditService;
            _logger = logger;
        }

        [HttpGet("media/{subreddit}")]
        public async Task<ActionResult<List<RedditMediaPost>>> GetSubredditMedia(
            string subreddit,
            [FromQuery] int limit = 25,
            [FromQuery] string timeFrame = "week")
        {
            try
            {
                // Validate input parameters
                if (string.IsNullOrWhiteSpace(subreddit))
                {
                    return BadRequest("Subreddit name is required");
                }

                if (limit <= 0 || limit > 100)
                {
                    limit = 25; // Default limit if invalid
                }

                var validTimeFrames = new[] { "hour", "day", "week", "month", "year", "all" };
                if (!validTimeFrames.Contains(timeFrame.ToLower()))
                {
                    timeFrame = "week"; // Default time frame if invalid
                }

                _logger.LogInformation($"Processing request for r/{subreddit} media, limit: {limit}, time frame: {timeFrame}");

                var mediaPosts = await _redditService.GetMediaPostsFromSubreddit(subreddit, limit, timeFrame);

                if (mediaPosts.Count == 0)
                {
                    return NotFound($"No media posts found in r/{subreddit}");
                }

                return Ok(mediaPosts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving media from r/{subreddit}");
                return StatusCode(500, new { error = "An error occurred while retrieving the media posts", details = ex.Message });
            }
        }

        [HttpGet("media/{subreddit}/filter/{mediaType}")]
        public async Task<ActionResult<List<RedditMediaPost>>> GetFilteredSubredditMedia(
            string subreddit,
            string mediaType,
            [FromQuery] int limit = 25,
            [FromQuery] string timeFrame = "week")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(subreddit))
                {
                    return BadRequest("Subreddit name is required");
                }

                if (string.IsNullOrWhiteSpace(mediaType))
                {
                    return BadRequest("Media type is required");
                }

                var validMediaTypes = new[] { "image", "video", "gif", "all" };
                if (!validMediaTypes.Contains(mediaType.ToLower()))
                {
                    return BadRequest($"Invalid media type. Valid types are: {string.Join(", ", validMediaTypes)}");
                }

                var mediaPosts = await _redditService.GetMediaPostsFromSubreddit(subreddit, limit, timeFrame);

                if (mediaType.ToLower() != "all")
                {
                    mediaPosts = mediaPosts.Where(p => p.MediaType == mediaType.ToLower()).ToList();
                }

                if (mediaPosts.Count == 0)
                {
                    return NotFound($"No {mediaType} media posts found in r/{subreddit}");
                }

                return Ok(mediaPosts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving {mediaType} media from r/{subreddit}");
                return StatusCode(500, new { error = $"An error occurred while retrieving {mediaType} media posts", details = ex.Message });
            }
        }
    }
}