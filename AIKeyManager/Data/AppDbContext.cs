using Microsoft.EntityFrameworkCore;
using AIKeyManager.Models;
using Request = AIKeyManager.Models.Request;

namespace AIKeyManager.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Provider> Providers { get; set; }
        public DbSet<AIModel> Models { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<UserSubscription> UserSubscriptions { get; set; }
        public DbSet<Credit> Credits { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<ApiKey> ApiKeys { get; set; }
        public DbSet<Request> ApiRequests { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Request>().ToTable("Requests");
            modelBuilder.Entity<AIModel>().ToTable("Models");
            modelBuilder.Entity<Transaction>().ToTable("Transactions");
            modelBuilder.Entity<ApiKey>().ToTable(tb => tb.HasTrigger("trg_ApiKey_Insert"));
            modelBuilder.Entity<Request>().ToTable(tb => tb.HasTrigger("trg_Request_DeductCredit"));

            modelBuilder.Entity<AIModel>().Property(m => m.CostPerRequest).HasPrecision(10, 6);
            modelBuilder.Entity<Request>().Property(r => r.CostCharged).HasPrecision(10, 6);
            modelBuilder.Entity<Credit>().Property(c => c.Balance).HasPrecision(10, 2);
            modelBuilder.Entity<Transaction>().Property(t => t.Amount).HasPrecision(10, 2);
            modelBuilder.Entity<Subscription>().Property(s => s.MonthlyCredit).HasPrecision(10, 2);

            base.OnModelCreating(modelBuilder);
        }
    }
}