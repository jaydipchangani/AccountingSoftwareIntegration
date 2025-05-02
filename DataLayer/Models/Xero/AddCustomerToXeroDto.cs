namespace WebApplication1.Models.Xero
{
    namespace WebApplication1.Dtos
    {
        public class AddCustomerToXeroDto
        {
            public string? DisplayName { get; set; }
            public string? GivenName { get; set; }
            public string? FamilyName { get; set; }
            public string? Email { get; set; }

            public List<PhoneDto>? Phones { get; set; }
            public List<AddressDto>? Addresses { get; set; }
        }


        public class PhoneDto
        {
            public string PhoneType { get; set; } = "MOBILE";
            public string? PhoneNumber { get; set; }
            public string? PhoneAreaCode { get; set; }
            public string? PhoneCountryCode { get; set; } // New field
        }

        public class AddressDto
        {
            public string AddressType { get; set; } = "STREET";
            public string? City { get; set; }
            public string? Region { get; set; }
            public string? PostalCode { get; set; }
            public string? Country { get; set; }
        }

        public class UpdateCustomerInXeroDto
        {
            public string ContactID { get; set; } = string.Empty; // Required
            public string? DisplayName { get; set; }
            public string? GivenName { get; set; }
            public string? FamilyName { get; set; }
            public string? Email { get; set; }

            public List<PhoneDto>? Phones { get; set; }
            public List<AddressDto>? Addresses { get; set; }
        }

    }

}
