using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Models.Xero
{
    public class InvoiceWrapper
    {
        public List<InvoiceDto> Invoices { get; set; } = new();
    }


}
