using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIKeyManager.Models
{
    public class AIModel
    {
        [Key]
        public int ModelId { get; set; }

        public int ProviderId { get; set; }

        [Required]
        [StringLength(100)]
        public string ModelName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public decimal CostPerRequest { get; set; } = 0.01m;

        public bool IsActive { get; set; } = true;

        [ForeignKey("ProviderId")]
        public virtual Provider Provider { get; set; }
    }
}