using System;
using System.ComponentModel.DataAnnotations;

namespace XeroIntegration.Models
{
    public class XeroToken
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string IdToken { get; set; }
        public string TokenType { get; set; }
        public string Scope { get; set; }
        public string TenantId { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
