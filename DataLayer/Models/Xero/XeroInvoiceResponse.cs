using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Models.Xero
{
    public class XeroInvoiceResponse
    {
        public List<XeroInvoice> Invoices { get; set; }
    }

    public class XeroInvoice
    {
        public string InvoiceID { get; set; }
        public string Status { get; set; }
        // Add more properties if needed
    }

}
