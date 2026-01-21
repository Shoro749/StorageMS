using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Models
{
    public class OutgoingItem
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, Column(TypeName = "decimal(10, 2)")]
        public decimal Quantity { get; set; }

        public OutgoingRequest Request { get; set; }

        public Product Product { get; set; }
    }
}
