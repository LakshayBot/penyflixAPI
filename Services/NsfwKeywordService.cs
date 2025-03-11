using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using pentyflixApi.Data;
using pentyflixApi.Models;

namespace pentyflixApi.Services
{
    public interface INsfwKeywordService
    {
        Task<List<string>> GetAllKeywordsAsync();
    }

    public class NsfwKeywordService : INsfwKeywordService
    {
        private readonly ApplicationDbContext _context;

        public NsfwKeywordService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> GetAllKeywordsAsync()
        {
            return await _context.NsfwKeywords
                .Select(k => k.Keyword)
                .ToListAsync();
        }
    }
}