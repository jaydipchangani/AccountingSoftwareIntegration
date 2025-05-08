using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

[Table("invoices")]
public class Invoice
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Platform { get; set; } = "Xero";

    [Required]
    public string QuickBooksId { get; set; } = string.Empty; // Xero: InvoiceID

    public string CustomerId { get; set; } = string.Empty;          // Xero: ContactID
    public string CustomerName { get; set; } = string.Empty;        // Xero: Contact.Name
    public string CustomerEmail { get; set; } = string.Empty;

    public string? DocNumber { get; set; }           // Xero: InvoiceNumber
    public string CustomerMemo { get; set; } = string.Empty;        // Xero: Reference
    public DateTime TxnDate { get; set; } = DateTime.MinValue;      // Xero: Date
    public DateTime DueDate { get; set; } = DateTime.MinValue;      // Xero: DueDate

    public string Store { get; set; } = string.Empty;

    public string BillingAddressJson { get; set; } = "{}";
    public string ShippingAddressJson { get; set; } = "{}";

    [NotMapped]
    public JsonDocument BillingAddress =>
        string.IsNullOrWhiteSpace(BillingAddressJson) ? JsonDocument.Parse("{}") : JsonDocument.Parse(BillingAddressJson);

    [NotMapped]
    public JsonDocument ShippingAddress =>
        string.IsNullOrWhiteSpace(ShippingAddressJson) ? JsonDocument.Parse("{}") : JsonDocument.Parse(ShippingAddressJson);

    public decimal Subtotal { get; set; } = 0;
    public decimal TotalAmt { get; set; } = 0;
    public decimal Balance { get; set; } = 0;

    public bool SendLater { get; set; } = false;
    public string PrintStatus { get; set; } = string.Empty;
    public string EmailStatus { get; set; } = string.Empty;
    public string SyncToken { get; set; } = string.Empty;

    public string XeroInvoiceType { get; set; } = string.Empty;
    public string XeroStatus { get; set; } = string.Empty;
    public string XeroBrandingThemeID { get; set; } = string.Empty;
    public string XeroCurrencyCode { get; set; } = string.Empty;
    public decimal XeroCurrencyRate { get; set; } = 1;
    public bool XeroIsDiscounted { get; set; } = false;
    public string XeroLineAmountTypes { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();


}



