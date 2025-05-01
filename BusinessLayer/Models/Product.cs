using System.ComponentModel.DataAnnotations;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace WebApplication1.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required] public string? Name { get; set; } 
        public string? Code { get; set; }
        public string? Description { get; set; }
        public string? Platform { get; set; }
         public string? Type { get; set; } // "Service" or "Inventory"
         public decimal? Price { get; set; }

         public string? IncomeAccount { get; set; }
        public string? AssetAccount { get; set; }
        public string? ExpenseAccount { get; set; }

        public decimal? QuantityOnHand { get; set; }
        public DateTime? AsOfDate { get; set; }

        public bool? IsActive { get; set; } = true;

        public string? QuickBooksItemId { get; set; }
        public string? QuickBooksUserId { get; set; }

        public string? SyncToken { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string? IncomeAccountId { get; set; }
        public string? AssetAccountId { get; set; }
        public string? ExpenseAccountId { get; set; }

        public bool IsTrackedAsInventory { get; set; }

        // Only populated if IsTrackedAsInventory == true
        public decimal? PurchaseUnitPrice { get; set; }
        public string? PurchaseCOGSAccountCode { get; set; }
        public string? PurchaseTaxType { get; set; }

        public decimal? SalesUnitPrice { get; set; }
        public string? SalesAccountCode { get; set; }
        public string? SalesTaxType { get; set; }
    }



    public class XeroItemsResponse
    {
        public List<XeroItem> Items { get; set; }
    }

    public class XeroItem
    {
        public string ItemID { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public string PurchaseDescription { get; set; }
        public bool IsTrackedAsInventory { get; set; }
        public decimal? QuantityOnHand { get; set; }
        public string InventoryAssetAccountCode { get; set; }

        public PurchaseDetails PurchaseDetails { get; set; }
        public SalesDetails SalesDetails { get; set; }
    }

    public class PurchaseDetails
    {
        public decimal? UnitPrice { get; set; }
        public string COGSAccountCode { get; set; }
        public string TaxType { get; set; }
    }

    public class SalesDetails
    {
        public decimal? UnitPrice { get; set; }
        public string AccountCode { get; set; }
        public string TaxType { get; set; }
    }

}
