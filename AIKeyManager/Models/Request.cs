using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIKeyManager.Models
{
    public class Request
    {
        [Key]
        public int RequestId { get; set; }

        public int ApiKeyId { get; set; }
        public int UserId { get; set; }
        public int ModelId { get; set; }

        public int TokensUsed { get; set; } = 0;

        public decimal CostCharged { get; set; } = 0;

        public DateTime RequestedAt { get; set; } = DateTime.Now;

        public int StatusCode { get; set; } = 200;

        [ForeignKey("ApiKeyId")]
        public virtual ApiKey ApiKey { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [ForeignKey("ModelId")]
        public virtual AIModel AIModel { get; set; }
    }
}