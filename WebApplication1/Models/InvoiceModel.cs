public class InvoiceRequest
{
    public List<Line> Line { get; set; }
    public Reference CustomerRef { get; set; }
}

public class Line
{
    public string DetailType { get; set; }
    public decimal Amount { get; set; }
    public SalesItemLineDetail SalesItemLineDetail { get; set; }
}

public class SalesItemLineDetail
{
    public Reference ItemRef { get; set; }
}

public class Reference
{
    public string value { get; set; }
}
