using AIKeyManager.Data;
using AIKeyManager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIKeyManager.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalApiKeys = await _context.ApiKeys.CountAsync();
            ViewBag.TotalRequests = await _context.ApiRequests.CountAsync();
            ViewBag.TotalProviders = await _context.Providers.CountAsync();
            return View();
        }

        // Providers
        public async Task<IActionResult> Providers()
        {
            var providers = await _context.Providers.ToListAsync();
            return View(providers);
        }

        [HttpPost]
        public async Task<IActionResult> AddProvider(string providerName, string description)
        {
            _context.Providers.Add(new Provider
            {
                ProviderName = providerName,
                Description = description,
                IsActive = true,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
            return RedirectToAction("Providers");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProvider(int id)
        {
            var provider = await _context.Providers.FindAsync(id);
            if (provider != null)
            {
                provider.IsActive = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Providers");
        }

        // Models
        public async Task<IActionResult> AIModels()
        {
            var models = await _context.Models
                .Include(m => m.Provider)
                .ToListAsync();
            ViewBag.Providers = await _context.Providers.Where(p => p.IsActive).ToListAsync();
            return View(models);
        }

        [HttpPost]
        public async Task<IActionResult> AddModel(int providerId, string modelName, string description, decimal costPerRequest)
        {
            _context.Models.Add(new AIModel
            {
                ProviderId = providerId,
                ModelName = modelName,
                Description = description,
                CostPerRequest = costPerRequest,
                IsActive = true
            });
            await _context.SaveChangesAsync();
            return RedirectToAction("AIModels");
        }

        // Users
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .ToListAsync();
            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Users");
        }
    }
}