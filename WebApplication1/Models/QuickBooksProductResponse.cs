namespace WebApplication1.Models
{
    public class QuickBooksProductResponse
    {
        public ItemDetail Item { get; set; }

        public class ItemDetail
        {
            public string Id { get; set; }
            public string SyncToken { get; set; }
        }
    }
}
