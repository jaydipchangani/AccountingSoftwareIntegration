using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using DataLayer.Models;
using WebApplication1.Models;
using WebApplication1.Models.Xero;

namespace WebApplication1.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<QuickBooksToken> QuickBooksTokens { get; set; }
        public DbSet<ChartOfAccount> ChartOfAccounts { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceLineItem> Lines { get; set; }
        public DbSet<InvoiceLineItem> InvoiceLineItems { get; set; }

        public DbSet<Bill> Bills { get; set; }
        public DbSet<BillLineItem> BillLineItems { get; set; }

        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<Item> Items { get; set; }

        public DbSet<CSVParse> CSVParses { get; set; }

        public DbSet<XeroToken> XeroTokens { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Add index constraints
            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.QuickBooksId)
                .IsUnique();

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.DocNumber)
                .IsUnique();

            // Value converter to ensure JSON strings are stored properly
            var jsonConverter = new ValueConverter<string?, string?>(
                v => v, // no change needed, we store string
                v => v
            );

            modelBuilder.Entity<Invoice>()
                .Property(e => e.BillingAddressJson)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<Invoice>()
                .Property(e => e.ShippingAddressJson)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<Invoice>()
                .HasMany(i => i.LineItems)
                .WithOne(li => li.Invoice)
                .HasForeignKey(li => li.InvoiceId);

            modelBuilder.Ignore<CurrencyRef>();

            base.OnModelCreating(modelBuilder);

        }
    }
}
