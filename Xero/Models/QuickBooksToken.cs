using System;
using System.ComponentModel.DataAnnotations;

namespace XeroIntegration.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

        public class QuickBooksToken
        {
            [Key]
            public int Id { get; set; }
 
            public string? QuickBooksUserId { get; set; }

            public string? RealmId { get; set; }

            public string AccessToken { get; set; }

            public string RefreshToken { get; set; }

            public string IdToken { get; set; }

            public string TokenType { get; set; }

            public double? ExpiresIn { get; set; }

            public int? XRefreshTokenExpiresIn { get; set; }

            public DateTime CreatedAt { get; set; }

            public DateTime? UpdatedAt { get; set; }
            public DateTime? ExpiresAtUtc { get; set; }

            public string? Scope { get; set; }

            public string? TenantId { get; set; }

            public DateTime? CreatedAtUtc { get; set; }

            public string? Company { get; set; }
        }
 
}

