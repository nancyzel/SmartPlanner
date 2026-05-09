using SmartPlanner.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartPlanner.Models
{
    public class ScheduleEntry
    {
        public long Id { get; set; }

        [Required]
        public long UserId { get; set; }

        [Required]
        public long ActivityId { get; set; }

        public ScheduleEntryStatus Status { get; set; }

        public GenerationSourceType GenerationSource { get; set; }

        [Range(0, 1)]
        public decimal? ConfidenceScore { get; set; }

        public DateTime StartAt { get; set; }

        public DateTime EndAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;

        public Activity Activity { get; set; } = null!;
    }
}