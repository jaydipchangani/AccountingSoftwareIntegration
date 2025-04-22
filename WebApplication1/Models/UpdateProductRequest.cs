using System.Text.Json.Serialization;

namespace WebApplication1.Models
{
    public class UpdateProductRequest
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Type { get; set; }

        public decimal UnitPrice { get; set; }

        public string? IncomeAccountId { get; set; }

        public string? AssetAccountId { get; set; }

        public string? ExpenseAccountId { get; set; }
    }
}
