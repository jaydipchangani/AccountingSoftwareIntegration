        using System;
        using System.Collections.Generic;
        using System.ComponentModel.DataAnnotations;
        using System.ComponentModel.DataAnnotations.Schema;
        using System.Text.Json;

        [Table("invoices")]
        public class Invoice
        {
            [Key]
            public Guid Id { get; set; } = Guid.NewGuid();

            [Required]
            public string QuickBooksId { get; set; }

            public string? CustomerId { get; set; }

            public string? DocNumber { get; set; }

            public string? CustomerName { get; set; }

            public string? CustomerEmail { get; set; }

            public DateTime? TxnDate { get; set; }

            public DateTime? DueDate { get; set; }

            public string? Store { get; set; }

            public string? BillingAddressJson { get; set; }
            public string? ShippingAddressJson { get; set; }

            [NotMapped]
            public JsonDocument? BillingAddress =>
                string.IsNullOrWhiteSpace(BillingAddressJson) ? null : JsonDocument.Parse(BillingAddressJson);

            [NotMapped]
            public JsonDocument? ShippingAddress =>
                string.IsNullOrWhiteSpace(ShippingAddressJson) ? null : JsonDocument.Parse(ShippingAddressJson);

            public decimal? Subtotal { get; set; }
            public decimal? TotalAmt { get; set; }
            public decimal? Balance { get; set; }
            public bool? SendLater { get; set; } = false;
            public string? CustomerMemo { get; set; }
            public string? PrintStatus { get; set; }
            public string? EmailStatus { get; set; }
            public string? SyncToken { get; set; }

            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
            public bool? IsActive { get; set; } = true;

            public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
        }
