using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class Incoming
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, Column(TypeName = "decimal(10, 2)")]
        public decimal Quantity { get; set; }

        public DateTime ReceivedAt { get; set; } = DateTime.Now;

        public Product Product { get; set; }

        public User ReceivedBy { get; set; }
    }
}
