namespace WebApplication1.Models
{
    public class QuickBooksResponse
    {
        public QueryResponse QueryResponse { get; set; }
    }

    public class QueryResponse
    {
        public List<QbAccount> Account { get; set; }
        public object Vendor { get; internal set; }
    }

    public class QbAccount
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string AccountType { get; set; }
        public string AccountSubType { get; set; }
    }

}
