using Microsoft.EntityFrameworkCore;
using XeroIntegration.Models.Xero;
using XeroIntegration.Models;


namespace XeroIntegration.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

      

        public DbSet<XeroToken> XeroTokens { get; set; }

        public DbSet<QuickBooksToken> QuickBooksTokens { get; set; }

        public DbSet<ChartOfAccount> ChartOfAccounts { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            

        }
    }
}
