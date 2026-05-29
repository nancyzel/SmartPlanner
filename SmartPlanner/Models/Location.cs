using System.ComponentModel.DataAnnotations;

namespace SmartPlanner.Models
{
    public class Location
    {
        public long Id { get; set; }

        [Required]
        public long UserId { get; set; }

        [Required(ErrorMessage = "Пожалуйста, укажите название места.")]
        [StringLength(100, ErrorMessage = "Название места не может превышать 100 символов.")]
        [Display(Name = "Название локации")]
        public string Name { get; set; } = string.Empty;

        [StringLength(150, ErrorMessage = "Адрес не может превышать 150 символов.")]
        [Display(Name = "Адрес")]
        public string? Address { get; set; }

        public TimeSpan? OpeningTime { get; set; }

        public TimeSpan? ClosingTime { get; set; }

        public User User { get; set; } = null!;

        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}
