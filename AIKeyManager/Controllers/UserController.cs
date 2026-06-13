using AIKeyManager.Data;
using AIKeyManager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AIKeyManager.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return value != null ? int.Parse(value) : 0;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var credit = await _context.Credits
                .FirstOrDefaultAsync(c => c.UserId == userId);

            var apiKeys = await _context.ApiKeys
                .Include(k => k.AIModel)
                .ThenInclude(m => m.Provider)
                .Where(k => k.UserId == userId && k.IsActive)
                .ToListAsync();

            var recentRequests = await _context.ApiRequests
                .Include(r => r.AIModel)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestedAt)
                .Take(5)
                .ToListAsync();

            ViewBag.Credit = credit?.Balance ?? 0;
            ViewBag.ApiKeys = apiKeys ?? new List<ApiKey>();
            ViewBag.RecentRequests = recentRequests ?? new List<Request>();

            return View();
        }

        public async Task<IActionResult> ApiKeys()
        {
            var userId = GetUserId();
            var keys = await _context.ApiKeys
                .Include(k => k.AIModel)
                .ThenInclude(m => m.Provider)
                .Where(k => k.UserId == userId)
                .ToListAsync();

            ViewBag.Models = await _context.Models
                .Include(m => m.Provider)
                .Where(m => m.IsActive)
                .ToListAsync();

            return View(keys);
        }

        [HttpPost]
        public async Task<IActionResult> GenerateApiKey(int modelId, string keyName)
        {
            var userId = GetUserId();

            var newKey = "ak-" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            newKey = newKey.Substring(0, 48);

            _context.ApiKeys.Add(new ApiKey
            {
                UserId = userId,
                ModelId = modelId,
                KeyValue = newKey,
                KeyName = keyName,
                IsActive = true,
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            return RedirectToAction("ApiKeys");
        }

        [HttpPost]
        public async Task<IActionResult> RevokeApiKey(int id)
        {
            var userId = GetUserId();
            var key = await _context.ApiKeys
                .FirstOrDefaultAsync(k => k.ApiKeyId == id && k.UserId == userId);

            if (key != null)
            {
                key.IsActive = false;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("ApiKeys");
        }

        public async Task<IActionResult> Search(string query, int? providerId, int? modelId)
        {
            var models = _context.Models
                .Include(m => m.Provider)
                .Where(m => m.IsActive);

            if (!string.IsNullOrEmpty(query))
                models = models.Where(m => m.ModelName.Contains(query) || m.Provider.ProviderName.Contains(query));

            if (providerId.HasValue)
                models = models.Where(m => m.ProviderId == providerId);

            ViewBag.Providers = await _context.Providers.Where(p => p.IsActive).ToListAsync();
            ViewBag.Query = query;
            ViewBag.ProviderId = providerId;

            return View(await models.ToListAsync());
        }
    }
}