using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace WebApplication1.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string? QuickBooksCustomerId { get; set; } = " ";

        [Required, MaxLength(50)]
        public string? QuickBooksUserId { get; set; } = " ";

        public string Company { get; set; }

        [MaxLength(150)]
        public string? DisplayName { get; set; }

        [MaxLength(150)]
        public string? CompanyName { get; set; } = " ";

        [MaxLength(50)]
        public string? GivenName { get; set; }

        [MaxLength(50)]
        public string? MiddleName { get; set; } = " ";

        [MaxLength(50)]
        public string? FamilyName { get; set; }

        [MaxLength(20)]
        public string? Title { get; set; } = " ";

        [MaxLength(20)]
        public string? Suffix { get; set; } = " ";

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(200)]
        public string? BillingLine1 { get; set; }

        [MaxLength(100)]
        public string? BillingCity { get; set; }

        [MaxLength(50)]
        public string? BillingState { get; set; }

        [MaxLength(20)]
        public string? BillingPostalCode { get; set; }

        [MaxLength(50)]
        public string? BillingCountry { get; set; }

        [MaxLength(200)]
        public string? ShippingLine1 { get; set; } = " ";

        [MaxLength(100)]
        public string? ShippingCity { get; set; } = " ";

        [MaxLength(50)]
        public string? ShippingState { get; set; } = " ";

        [MaxLength(20)]
        public string? ShippingPostalCode { get; set; } = " ";

        [MaxLength(50)]
        public string? ShippingCountry { get; set; } = " ";

        [MaxLength(50)]
        public string? PreferredDeliveryMethod { get; set; } = " ";

        [MaxLength(150)]
        public string? PrintOnCheckName { get; set; } = " ";

        public bool Active { get; set; }

        public decimal Balance { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; } = " ";

        public DateTime? QuickBooksCreateTime { get; set; } 

        public DateTime? QuickBooksLastUpdateTime { get; set; } 

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // Additional fields for Xero data
        public string? ContactID { get; set; }

        [MaxLength(50)]
        public string? TaxNumber { get; set; } = " ";

        public string? BankAccountDetails { get; set; } = " ";

        [MaxLength(200)]
        public string? Website { get; set; } = " ";

        public string? XeroNetworkKey { get; set; } = " ";

        public string? Phones { get; set; } = " ";

        public string? Addresses { get; set; } = " ";

        public decimal? Discount { get; set; } = 0 ;
    }


    public class XeroContactResponse
    {
        public List<XeroContact> Contacts { get; set; }
    }

    public class XeroContact
    {
        public string ContactID { get; set; }
        public string Name { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmailAddress { get; set; }

        [JsonProperty("ContactStatus")]
        public string ContactStatus { get; set; }
        public List<XeroPhone> Phones { get; set; }
        public List<XeroAddress> Addresses { get; set; }
    }

    public class XeroPhone
    {
        public string PhoneNumber { get; set; }
    }

    public class XeroAddress
    {
        public string AddressType { get; set; }
        public string AddressLine { get; set; }
        public string City { get; set; }
        public string Region { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
    }
}


