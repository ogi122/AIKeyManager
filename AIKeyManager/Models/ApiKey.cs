using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIKeyManager.Models
{
    public class ApiKey
    {
        [Key]
        public int ApiKeyId { get; set; }

        public int UserId { get; set; }
        public int ModelId { get; set; }

        [Required]
        [StringLength(64)]
        public string KeyValue { get; set; }

        [StringLength(100)]
        public string KeyName { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastUsedAt { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [ForeignKey("ModelId")]
        public virtual AIModel AIModel { get; set; }
    }
}
