using SmartPlanner.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartPlanner.Models
{
    public class ActivityLog
    {
        public long Id { get; set; }

        [Required]
        public long UserId { get; set; }

        public ActionType Action { get; set; }

        [Required]
        [StringLength(50)]
        public string EntityType { get; set; } = string.Empty;

        public long EntityId { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
    }
}