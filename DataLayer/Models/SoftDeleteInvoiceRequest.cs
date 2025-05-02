namespace WebApplication1.Models
{
    public class SoftDeleteInvoiceRequest
    {
        public string Id { get; set; } = string.Empty;
        public string SyncToken { get; set; } = string.Empty;
    }
}
