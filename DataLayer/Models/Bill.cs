using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace WebApplication1.Models
{
    public class Bill
    {
        public int Id { get; set; }

        [Required]
        public string QboBillId { get; set; }

        [Required]
        public int VendorId { get; set; }
        public Vendor Vendor { get; set; }

        [StringLength(500)]
        public string? VendorAddress { get; set; }

        [StringLength(100)]
        public string? Currency { get; set; }

        public string? DocNumber { get; set; }

        [Required]
        public DateTime TxnDate { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmt { get; set; }

        [StringLength(100)]
        public string? APAccountName { get; set; }

        public string? SyncToken { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<BillLineItem> LineItems { get; set; } = new();
    }

    public class BillLineItem
    {
        public int Id { get; set; }

        public int BillId { get; set; }

        [JsonIgnore]
        public Bill Bill { get; set; }

        public int? AccountId { get; set; }
        public int? ItemId { get; set; }

        public string? DetailType { get; set; }

        [StringLength(4000)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
    }

}