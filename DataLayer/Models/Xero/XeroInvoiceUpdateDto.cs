using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Models.Xero
{
    public class XeroInvoiceUpdateDto
    {
        public string InvoiceId { get; set; }
        public List<XeroInvoiceLineItemDto> LineItems { get; set; }
        public DateTime Date { get; set; }
        public DateTime DueDate { get; set; }
        public string Reference { get; set; }
        public string? Status { get; set; } // Optional: "DRAFT", "SUBMITTED", etc.
    }

    public class XeroInvoiceLineItemDto
    {
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitAmount { get; set; }
        public string AccountCode { get; set; }
        public string TaxType { get; set; }
        public decimal LineAmount { get; set; }
    }

}
