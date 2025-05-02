namespace WebApplication1.Models
{
    public class ProductDto
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public string Type { get; set; } // "Service" or "Inventory"
        public decimal Price { get; set; }

        // For QuickBooks API (IDs)
        public string IncomeAccountId { get; set; }
        public string? AssetAccountId { get; set; }
        public string? ExpenseAccountId { get; set; }

        // For local display (names)
        public string IncomeAccount { get; set; }
        public string? AssetAccount { get; set; }
        public string? ExpenseAccount { get; set; }

        // Inventory-specific fields
        public int? QuantityOnHand { get; set; }
        public DateTime? AsOfDate { get; set; }
    }
}
