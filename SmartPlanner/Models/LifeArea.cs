using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SmartPlanner.Models
{
    public class LifeArea
    {
        public long Id { get; set; }

        [Required]
        public long UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(7)]
        public string? Color { get; set; }

        [Range(1, 5)]
        public int Priority { get; set; }

        public bool IsActive { get; set; } = true;

        public User User { get; set; } = null!;

        public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    }
}
