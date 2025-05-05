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

    [Required]
    public string ProductId { get; set; } = string.Empty;

    [Required]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public decimal Quantity { get; set; } = 0;

    [Required]
    public decimal Rate { get; set; } = 0;

    [Required]
    public decimal Amount { get; set; } = 0;

    [Required]
    public int LineNum { get; set; } = 0;

    [Required]
    public string DetailType { get; set; } = "SalesItemLineDetail";

    [Required]
    public string ItemRef { get; set; } = string.Empty;

    [Required]
    public string ItemName { get; set; } = string.Empty;

    // Xero-specific fields
    [Required]
    public string XeroLineItemId { get; set; } = string.Empty;

    [Required]
    public string XeroAccountCode { get; set; } = string.Empty;

    [Required]
    public string XeroAccountId { get; set; } = string.Empty;

    [Required]
    public string XeroTaxType { get; set; } = string.Empty;

    [Required]
    public decimal XeroTaxAmount { get; set; } = 0;

    [Required]
    public decimal XeroDiscountRate { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public string PlatformLineItem { get; set; } = "Xero";  // or default value
}
