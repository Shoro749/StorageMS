using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class Product
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, StringLength(128)]
        public string Name { get; set; }

        [StringLength(256)]
        public string Description { get; set; }

        [Required, StringLength(32)]
        public string Unit { get; set; }

        [Required, Column(TypeName = "decimal(10, 2)")]
        public decimal Stock { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
