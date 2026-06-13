using System;
using System.ComponentModel.DataAnnotations;

namespace AIKeyManager.Models
{
    public class Provider
    {
        [Key]
        public int ProviderId { get; set; }

        [Required]
        [StringLength(100)]
        public string ProviderName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [StringLength(300)]
        public string LogoUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
