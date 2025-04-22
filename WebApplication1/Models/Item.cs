namespace WebApplication1.Models
{
    public class Item
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string QboItemId { get; set; }

        public string? ProductId { get; set; }

        public string? ProductName { get; set; }

        public string? Description { get; set; }

        public int? Quantity { get; set; }

        public decimal? Rate { get; set; }

        public decimal? Amount { get; set; }

        public int? LineNum { get; set; }

        public string? DetailType { get; set; } = "SalesItemLineDetail";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string? ItemRef { get; set; } // maps to ItemRef.value from QuickBooks
        public string? ItemName { get; set; }
    }
}
