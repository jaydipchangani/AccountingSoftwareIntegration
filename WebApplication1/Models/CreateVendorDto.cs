using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class CreateVendorDto
    {
        [Required]
        public string DisplayName { get; set; } = null!;

        public string? PrimaryEmailAddr { get; set; }
        public string? PrimaryPhone { get; set; }
        public string? BillAddrLine1 { get; set; }
        public string? BillAddrCity { get; set; }
        public string? BillAddrPostalCode { get; set; }
        public string? WebAddr { get; set; }
    }
}
