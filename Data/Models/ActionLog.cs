using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class ActionLog
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, StringLength(256)]
        public string Action { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public User User { get; set; }
    }
}
