namespace WebApplication1.Models
{
    public class CustomerDto
    {
        // Required fields
        public string DisplayName { get; set; }
        public string CompanyName { get; set; }
        public string Phone { get; set; }

        // Optional fields from your form
        public string? Email { get; set; }
        public string? BillingLine1 { get; set; }
        public string? BillingCity { get; set; }
        public string? BillingState { get; set; }
        public string? BillingPostalCode { get; set; }
        public string? BillingCountry { get; set; }
    }
}
