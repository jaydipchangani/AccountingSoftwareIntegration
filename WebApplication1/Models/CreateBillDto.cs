using System.ComponentModel.DataAnnotations;

public class CreateBillDto
{

    [Required]
    public int VendorId { get; set; }

    [Required]
    public string DocNumber { get; set; }

    [Required]
    public DateTime TxnDate { get; set; }

    [Required]
    public DateTime DueDate { get; set; }

    [Required]
    [MinLength(1)]
    public List<BillLineItemDto> LineItems { get; set; }
}


public class UpdateBillDto
    {
        [Required]
        public string Id { get; set; } // QboBillId

        [Required]
        public int VendorId { get; set; }

        public string? SyncToken { get; set; }

        public string DocNumber { get; set; }
        public DateTime? TxnDate { get; set; }
        public DateTime? DueDate { get; set; }

        public List<BillLineItemDto> LineItems { get; set; }
    }

    public class BillLineItemDto
    {
        [Required]
        public string DetailType { get; set; } // "AccountBasedExpenseLineDetail" or "ItemBasedExpenseLineDetail"

        // For AccountBasedExpenseLineDetail
        public int? AccountId { get; set; }

        // For ItemBasedExpenseLineDetail
        public int? ItemId { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }

        public string Description { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }
    }
