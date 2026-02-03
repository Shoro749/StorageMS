using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Models
{
    public class OutgoingRequest
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, StringLength(128)]
        public string Status { get; set; }

        [StringLength(256)]
        public string Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public User CreatedBy { get; set; }

        public virtual ICollection<OutgoingItem> Items { get; set; } = new HashSet<OutgoingItem>();
    }
}
