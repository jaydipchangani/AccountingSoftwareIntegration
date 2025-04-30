public class Vendor
{
    public int Id { get; set; }
    public string VId { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public bool Active { get; set; }
    public bool Vendor1099 { get; set; }
    public decimal Balance { get; set; }

    public string? PrimaryEmailAddr { get; set; } // Nullable
    public string? PrimaryPhone { get; set; }     // Nullable
    public string? CurrencyValue { get; set; }    // Nullable
    public string? CurrencyName { get; set; }     // Nullable
    public string? BillAddrLine1 { get; set; }    // Nullable
    public string? BillAddrCity { get; set; }     // Nullable
    public string? BillAddrPostalCode { get; set; } // Nullable
    public string? SyncToken { get; set; }        // Nullable
    public string? V4IDPseudonym { get; set; }    // Nullable
    public string? WebAddr { get; set; }          // Nullable

    public DateTime CreateTime { get; set; }
    public DateTime LastUpdatedTime { get; set; }
}
