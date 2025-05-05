using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Models.Xero
{
    public class XeroInvoiceCreateDto
    {
        public string ContactId { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public DateTime DueDate { get; set; }
        public List<XeroLineItemDto> LineItems { get; set; } = new();
    }

    public class XeroLineItemDto
    {
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitAmount { get; set; }
        public string AccountCode { get; set; } = "200";
        public string TaxType { get; set; } = "NONE";
        public decimal LineAmount { get; set; }
    }
}
