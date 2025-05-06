using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Models
{
    [Table("SyncState")]
    public class SyncState
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string EntityName { get; set; } = string.Empty; // e.g., "Invoices"

        [Required]
        [MaxLength(50)]
        public string Platform { get; set; } = string.Empty;   // e.g., "Xero", "QuickBooks"

        [Required]
        public int LastSyncedPage { get; set; } = 0;

        [Required]
        public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
