using System.ComponentModel.DataAnnotations;
using SmartPlanner.Models.Enums;

namespace SmartPlanner.Models
{
    public abstract class Activity
    {
        public long Id { get; set; }

        [Required]
        public long UserId { get; set; }

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ActivityType Type { get; set; }

        public long? LifeAreaId { get; set; }

        [Range(1, 5)]
        public int EnergyRequired { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;

        public LifeArea? LifeArea { get; set; }

        public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();
    }
}
