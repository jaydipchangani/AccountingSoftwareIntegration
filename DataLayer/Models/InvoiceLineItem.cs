// File: Models/InvoiceLineItem.cs

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("invoice_line_items")]
public class InvoiceLineItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid InvoiceId { get; set; }

    [ForeignKey("InvoiceId")]
    public Invoice Invoice { get; set; } = null!;

    public string? ProductId { get; set; }       // Xero: ItemCode
    public string? ProductName { get; set; }     // Xero: Item.Name
    public string? Description { get; set; }     // Xero: Description
    public int? Quantity { get; set; }           // Xero: Quantity
    public decimal? Rate { get; set; }           // Xero: UnitAmount
    public decimal? Amount { get; set; }         // Xero: LineAmount
    public int? LineNum { get; set; }
    public string? DetailType { get; set; } = "SalesItemLineDetail";

    public string? ItemRef { get; set; }         // Xero: ItemCode
    public string? ItemName { get; set; }        // Xero: Item.Name

    // Xero-specific fields
    public string? XeroLineItemId { get; set; }  // LineItemID
    public string? XeroAccountCode { get; set; } // AccountCode
    public string? XeroAccountId { get; set; }   // AccountID
    public string? XeroTaxType { get; set; }     // TaxType
    public decimal? XeroTaxAmount { get; set; }  // TaxAmount
    public decimal? XeroDiscountRate { get; set; } // DiscountRate

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string PlatformLineItem { get; set; }
}
