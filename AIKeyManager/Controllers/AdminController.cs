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
            ViewBag.ActiveApiKeys = await _context.ApiKeys.CountAsync(k => k.IsActive == true);
            ViewBag.ActiveProviders = await _context.Providers.CountAsync(p => p.IsActive == true);
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
        public async Task<IActionResult> ToggleProviderStatus(int id)
        {
            var provider = await _context.Providers.FindAsync(id);
            if (provider != null)
            {
                provider.IsActive = !provider.IsActive;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Providers");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProvider(int id)
        {
            var provider = await _context.Providers.FindAsync(id);
            if (provider == null) return RedirectToAction("Providers");

            try
            {
                _context.Providers.Remove(provider);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Provider je obrisan.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = $"Ne možeš obrisati '{provider.ProviderName}' jer ima povezane modele. Prvo obriši ili premjesti te modele, ili ga samo deaktiviraj.";
            }

            return RedirectToAction("Providers");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return RedirectToAction("Users");

            try
            {
                // 1. Nadji sve API keyeve korisnika
                var userApiKeys = await _context.ApiKeys
                    .Where(k => k.UserId == id)
                    .ToListAsync();

                var apiKeyIds = userApiKeys.Select(k => k.ApiKeyId).ToList();

                // 2. Obrisi sve requestove vezane za te keyeve
                var userRequests = await _context.ApiRequests
                    .Where(r => apiKeyIds.Contains(r.ApiKeyId) || r.UserId == id)
                    .ToListAsync();

                // 3. Obrisi sve API keyeve
                _context.ApiKeys.RemoveRange(userApiKeys);

                // 4. Obrisi transakcije korisnika
                var userTransactions = await _context.Transactions
                    .Where(t => t.UserId == id)
                    .ToListAsync();
                _context.Transactions.RemoveRange(userTransactions);

                // 5. Obrisi kredit korisnika
                var userCredit = await _context.Credits
                    .FirstOrDefaultAsync(c => c.UserId == id);
                if (userCredit != null)
                    _context.Credits.Remove(userCredit);

                // 6. Obrisi subscription korisnika
                var userSubscriptions = await _context.UserSubscriptions
                    .Where(s => s.UserId == id)
                    .ToListAsync();
                _context.UserSubscriptions.RemoveRange(userSubscriptions);

                // 7. Obrisi samog korisnika
                _context.Users.Remove(user);

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Korisnik '{user.Username}' je uspješno obrisan.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Greška: {ex.Message}";
            }

            return RedirectToAction("Users");
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

        [HttpPost]
        public async Task<IActionResult> ToggleModel(int id)
        {
            var model = await _context.Models.FindAsync(id);
            if (model != null)
            {
                model.IsActive = !model.IsActive;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("AIModels");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteModel(int id)
        {
            var model = await _context.Models.FindAsync(id);
            if (model == null) return RedirectToAction("AIModels");

            try
            {
                _context.Models.Remove(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Model '{model.ModelName}' je obrisan.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = $"Ne možeš obrisati '{model.ModelName}' jer ima povezane API keyeve. Prvo deaktiviraj model.";
            }

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