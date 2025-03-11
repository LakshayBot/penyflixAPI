using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using pentyflixApi.Services;

namespace pentyflixApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NsfwKeywordsController : ControllerBase
    {
        private readonly INsfwKeywordService _keywordService;

        public NsfwKeywordsController(INsfwKeywordService keywordService)
        {
            _keywordService = keywordService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<string>>> GetAllKeywords()
        {
            var keywords = await _keywordService.GetAllKeywordsAsync();
            return Ok(keywords);
        }
    }
}