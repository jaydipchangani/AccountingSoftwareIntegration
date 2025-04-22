using System.ComponentModel.DataAnnotations;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace WebApplication1.Models
{
    public class Product
    {

        public int Id { get; set; }

        [Required] public string Name { get; set; }
        public string Description { get; set; }

        [Required] public string Type { get; set; } // "Service" or "Inventory"
        [Required] public decimal Price { get; set; }

        // Account names stored locally
        [Required] public string IncomeAccount { get; set; }
        public string? AssetAccount { get; set; }
        public string? ExpenseAccount { get; set; }

        // Inventory-specific fields
        public decimal? QuantityOnHand { get; set; }
        public DateTime? AsOfDate { get; set; }

        public bool IsActive { get; set; } = true;

        // QuickBooks info
        public string? QuickBooksItemId { get; set; }
        public string? QuickBooksUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string IncomeAccountId { get;  set; }
        public string? AssetAccountId { get;  set; }
        public string? ExpenseAccountId { get;  set; }
    }
}
