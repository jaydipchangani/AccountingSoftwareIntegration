using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Models.Xero
{
    public class UpdateInvoiceRequest
    {
        public List<InvoiceUpdate> Invoices { get; set; } = new List<InvoiceUpdate>();
    }

    public class InvoiceUpdate
    {
        public string InvoiceID { get; set; }
        public string Reference { get; set; }
        public string Type { get; set; }
        public Contact Contact { get; set; }
        public List<LineItem> LineItems { get; set; } = new List<LineItem>();
    }

    public class Contact
    {
        public string ContactID { get; set; }
    }

    public class LineItem
    {
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitAmount { get; set; }
        public string AccountCode { get; set; }
        public string TaxType { get; set; }
        public decimal LineAmount { get; set; }
    }
}
