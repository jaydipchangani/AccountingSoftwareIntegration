using Newtonsoft.Json;

namespace WebApplication1.Models
{
    public class QuickBooksInvoiceResponse
    {
        [JsonProperty("QueryResponse")]
        public InvoiceQueryResponse QueryResponse { get; set; }
    }

    public class InvoiceQueryResponse
    {
        [JsonProperty("Invoice")]
        public List<Invoice> Invoices { get; set; }
    }
}
