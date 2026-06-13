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
            base.OnModelCreating(modelBuilder);
        }
    }
}