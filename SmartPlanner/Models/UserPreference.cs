using System.ComponentModel.DataAnnotations;

namespace SmartPlanner.Models
{
    public class UserPreference
    {
        public long Id { get; set; }

        [Required]
        public long UserId { get; set; }

        public TimeSpan? WorkStartTime { get; set; }

        public TimeSpan? WorkEndTime { get; set; }

        public TimeSpan? PeakProductivityStart { get; set; }

        public TimeSpan? PeakProductivityEnd { get; set; }

        [Range(1, 20)]
        public int MaxTasksPerDay { get; set; }

        [Range(1, 180)]
        public int BreakDuration { get; set; }

        [Range(15, 240)]
        public int PreferredBreakInterval { get; set; }

        public User User { get; set; } = null!;
    }
}