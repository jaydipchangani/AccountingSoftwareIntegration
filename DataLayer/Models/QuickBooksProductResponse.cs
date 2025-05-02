namespace WebApplication1.Models
{
    public class QuickBooksProductResponse
    {
        public QuickBooksProduct Item { get; set; }
    }

    public class QuickBooksProduct
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SyncToken { get; set; }
        // Add more fields if needed
    }

    public class QuickBooksQueryResponse
    {
        public QueryResponses QueryResponse { get; set; }
    }

    public class QueryResponses
    {
        public List<QuickBooksProduct> Item { get; set; }
    }

}
