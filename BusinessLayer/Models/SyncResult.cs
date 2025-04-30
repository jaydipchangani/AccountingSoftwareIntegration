namespace WebApplication1.Models
{
    public class SyncResult
    {
        public string Identifier { get; set; } // e.g., InvoiceNumber, CustomerName, ItemName
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
