using AIKeyManager.Data;
using AIKeyManager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
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

        // Ova metoda čita UserId iz korisnikovog login cookie-ja (claims).
        // Kada se korisnik uloguje, u AuthController smo zapisali UserId u Claims.
        // Ovdje ga čitamo nazad da znamo koji je trenutno ulogovani korisnik.
        private int GetUserId()
        {
            string userIdAsText = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userIdAsText == null)
            {
                return 0;
            }

            int userId = int.Parse(userIdAsText);
            return userId;
        }

        // Prikazuje glavnu stranicu korisnika nakon logina.
        public async Task<IActionResult> Dashboard()
        {
            int userId = GetUserId();

            if (userId == 0)
            {
                return RedirectToAction("Login", "Auth");
            }

            Credit credit = await _context.Credits
                .FirstOrDefaultAsync(c => c.UserId == userId);

            List<ApiKey> apiKeys = await _context.ApiKeys
                .Include(k => k.AIModel)
                .ThenInclude(m => m.Provider)
                .Where(k => k.UserId == userId && k.IsActive == true)
                .ToListAsync();

            List<Request> recentRequests = await _context.ApiRequests
                .Include(r => r.AIModel)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestedAt)
                .Take(5)
                .ToListAsync();

            if (credit != null)
            {
                ViewBag.Credit = credit.Balance;
            }
            else
            {
                ViewBag.Credit = 0;
            }

            ViewBag.ApiKeys = apiKeys;
            ViewBag.RecentRequests = recentRequests;

            decimal totalSpentResult = 0;

            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT dbo.fn_GetUserTotalSpent(@UserId)";
                command.Parameters.Add(new SqlParameter("@UserId", userId));

                var result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    totalSpentResult = (decimal)result;
                }
            }

            ViewBag.TotalSpent = totalSpentResult;

            return View();
        }

        // Prikazuje listu API keyeva korisnika i formu za generisanje novog.
        public async Task<IActionResult> ApiKeys()
        {
            int userId = GetUserId();

            List<ApiKey> keys = await _context.ApiKeys
                .Include(k => k.AIModel)
                .ThenInclude(m => m.Provider)
                .Where(k => k.UserId == userId)
                .ToListAsync();

            List<AIModel> activeModels = await _context.Models
                .Include(m => m.Provider)
                .Where(m => m.IsActive == true)
                .ToListAsync();

            ViewBag.Models = activeModels;

            return View(keys);
        }

        // Generiše novi API key. Umjesto da C# kod sam pravi key i upisuje ga,
        // poziva se stored procedura sp_GenerateApiKey koja je definisana u bazi.
        // Procedura sama provjerava da li korisnik i model postoje, generiše key,
        // i upisuje ga u tabelu ApiKeys.
        [HttpPost]
        public async Task<IActionResult> GenerateApiKey(int modelId, string keyName)
        {
            int userId = GetUserId();

            SqlParameter userIdParameter = new SqlParameter("@UserId", userId);
            SqlParameter modelIdParameter = new SqlParameter("@ModelId", modelId);
            SqlParameter keyNameParameter = new SqlParameter("@KeyName", keyName);

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_GenerateApiKey @UserId, @ModelId, @KeyName",
                    userIdParameter,
                    modelIdParameter,
                    keyNameParameter
                );
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
            }

            return RedirectToAction("ApiKeys");
        }

        // Deaktivira (revoke) postojeći API key. Ovo ostaje kao direktna
        // EF Core izmjena jer je jednostavna operacija (samo promjena jednog polja).
        [HttpPost]
        public async Task<IActionResult> RevokeApiKey(int id)
        {
            int userId = GetUserId();

            ApiKey key = await _context.ApiKeys
                .FirstOrDefaultAsync(k => k.ApiKeyId == id && k.UserId == userId);

            if (key != null)
            {
                key.IsActive = false;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("ApiKeys");
        }

        // Simulira korištenje API keya - kao da je AI stvarno odradio jedan poziv.
        // Ovo ubacuje red u Requests tabelu, a NAŠ TRIGGER (trg_Request_DeductCredit)
        // se automatski pokrene i oduzme kredit korisniku.
        [HttpPost]
        public async Task<IActionResult> SimulateRequest(int apiKeyId)
        {
            int userId = GetUserId();

            ApiKey apiKey = await _context.ApiKeys
                .Include(k => k.AIModel)
                .FirstOrDefaultAsync(k => k.ApiKeyId == apiKeyId && k.UserId == userId && k.IsActive == true);

            if (apiKey == null)
            {
                TempData["Error"] = "API key nije pronađen.";
                return RedirectToAction("ApiKeys");
            }

            Credit credit = await _context.Credits
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (credit != null && credit.Balance < apiKey.AIModel.CostPerRequest)
            {
                TempData["Error"] = "Nemate dovoljno kredita za ovaj poziv.";
                return RedirectToAction("ApiKeys");
            }

            Random random = new Random();
            int tokensUsed = random.Next(50, 2000);

            Request newRequest = new Request
            {
                ApiKeyId = apiKey.ApiKeyId,
                UserId = userId,
                ModelId = apiKey.ModelId,
                TokensUsed = tokensUsed,
                CostCharged = apiKey.AIModel.CostPerRequest,
                RequestedAt = DateTime.Now,
                StatusCode = 200
            };

            apiKey.LastUsedAt = DateTime.Now;

            _context.ApiRequests.Add(newRequest);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Request simuliran! Tokeni: " + tokensUsed;

            return RedirectToAction("ApiKeys");
        }

        // Pretraga modela - i brza pretraga (samo query) i detaljna (query + provider filter).
        public async Task<IActionResult> Search(string query, int? providerId, int? modelId)
        {
            IQueryable<AIModel> models = _context.Models
                .Include(m => m.Provider)
                .Where(m => m.IsActive == true);

            if (!string.IsNullOrEmpty(query))
            {
                models = models.Where(m =>
                    m.ModelName.Contains(query) ||
                    m.Provider.ProviderName.Contains(query));
            }

            if (providerId.HasValue)
            {
                models = models.Where(m => m.ProviderId == providerId.Value);
            }

            List<Provider> activeProviders = await _context.Providers
                .Where(p => p.IsActive == true)
                .ToListAsync();

            ViewBag.Providers = activeProviders;
            ViewBag.Query = query;
            ViewBag.ProviderId = providerId;

            List<AIModel> results = await models.ToListAsync();

            return View(results);
        }
    }
}