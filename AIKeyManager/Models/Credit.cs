using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIKeyManager.Models
{
    public class Credit
    {
        [Key]
        public int CreditId { get; set; }

        public int UserId { get; set; }

        public decimal Balance { get; set; } = 0.00m;

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }
}
