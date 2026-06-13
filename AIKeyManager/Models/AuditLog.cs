using System;
using System.ComponentModel.DataAnnotations;

namespace AIKeyManager.Models
{
    public class AuditLog
    {
        [Key]
        public int AuditId { get; set; }

        [Required]
        [StringLength(100)]
        public string TableName { get; set; }

        [Required]
        [StringLength(50)]
        public string Action { get; set; }

        public int? AffectedId { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string PerformedBy { get; set; }
    }
}