using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Models.Xero
{
    public class InvoiceDto
    {
        public Guid CustomerId { get; set; }
        public string DocNumber { get; set; } = string.Empty;
        public DateTime TxnDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TotalAmt { get; set; }
        public decimal Balance { get; set; }
        public bool SendLater { get; set; }
        public string XeroInvoiceType { get; set; } = string.Empty;
        public string XeroStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<LineItemDto> LineItems { get; set; } = new();
    }

    public class LineItemDto
    {
        public Guid Id { get; set; }
        public Guid InvoiceId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
        public string XeroAccountCode { get; set; } = string.Empty;
        public string XeroTaxType { get; set; } = string.Empty;
        public decimal XeroTaxAmount { get; set; }
        public decimal XeroDiscountRate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
