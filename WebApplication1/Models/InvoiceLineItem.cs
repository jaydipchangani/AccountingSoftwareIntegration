    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("invoice_line_items")]
    public class InvoiceLineItem
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid InvoiceId { get; set; }

        [ForeignKey("InvoiceId")]
        public Invoice Invoice { get; set; } = null!;

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
