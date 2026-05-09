using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SmartPlanner.Models
{
    public class User
    {
        public long Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(30)]
        public string? ShortName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string Salt { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Timezone { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Activity> Activities { get; set; } = new List<Activity>();

        public ICollection<LifeArea> LifeAreas { get; set; } = new List<LifeArea>();

        public ICollection<Location> Locations { get; set; } = new List<Location>();

        public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();

        public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();

        public UserPreference? UserPreference { get; set; }
    }
}
