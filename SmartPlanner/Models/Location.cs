using System.ComponentModel.DataAnnotations;

namespace SmartPlanner.Models
{
    public class Location
    {
        public long Id { get; set; }

        [Required]
        public long UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(150)]
        public string? Address { get; set; }

        public TimeSpan? OpeningTime { get; set; }

        public TimeSpan? ClosingTime { get; set; }

        public User User { get; set; } = null!;

        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}