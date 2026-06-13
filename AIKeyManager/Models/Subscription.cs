using System.ComponentModel.DataAnnotations;

namespace AIKeyManager.Models
{
    public class Subscription
    {
        [Key]
        public int SubscriptionId { get; set; }

        [Required]
        [StringLength(50)]
        public string PlanName { get; set; }

        public decimal MonthlyCredit { get; set; }

        public int MaxApiKeys { get; set; }

        [StringLength(300)]
        public string Description { get; set; }
    }
}
