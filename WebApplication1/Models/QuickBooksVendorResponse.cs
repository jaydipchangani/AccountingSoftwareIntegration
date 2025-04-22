public class QuickBooksVendorResponse
{
    public QueryResponse QueryResponse { get; set; }
}

public class QueryResponse
{
    public List<QBV> Vendor { get; set; }
}

public class QBV
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public bool Active { get; set; }
    public bool Vendor1099 { get; set; }
    public decimal Balance { get; set; }
    public CurrencyRef CurrencyRef { get; set; }
    public BillAddr BillAddr { get; set; }
    public string SyncToken { get; set; }
    public string V4IDPseudonym { get; set; }
    public PrimaryPhone PrimaryPhone { get; set; }
    public PrimaryEmailAddr PrimaryEmailAddr { get; set; }
    public WebAddr WebAddr { get; set; }
    public MetaData MetaData { get; set; }
}

public class CurrencyRef { public string Value { get; set; } public string Name { get; set; } }
public class BillAddr { public string Line1 { get; set; } public string City { get; set; } public string PostalCode { get; set; } }
public class PrimaryPhone { public string FreeFormNumber { get; set; } }
public class PrimaryEmailAddr { public string Address { get; set; } }
public class WebAddr { public string URI { get; set; } }
public class MetaData { public DateTime? CreateTime { get; set; } public DateTime? LastUpdatedTime { get; set; } }
