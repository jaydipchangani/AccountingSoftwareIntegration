using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class QuickBooksToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string QuickBooksUserId { get; set; }

        [Required]
        public string RealmId { get; set; }

        [Required]
        public string AccessToken { get; set; }

        public string RefreshToken { get; set; }

        public string IdToken { get; set; }

        public string TokenType { get; set; }

        public int ExpiresIn { get; set; }

        public int? XRefreshTokenExpiresIn { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
