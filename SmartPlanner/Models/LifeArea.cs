using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SmartPlanner.Models
{
    public class LifeArea
    {
        public long Id { get; set; }

        [Required]
        public long UserId { get; set; }

        [Required(ErrorMessage = "Пожалуйста, введите название сферы жизни.")]
        [StringLength(100, ErrorMessage = "Название не может превышать 100 символов.")]
        [Display(Name = "Название сферы жизни")]
        public string Name { get; set; } = string.Empty;

        [StringLength(
            7,
            ErrorMessage = "Код цвета должен быть в формате HEX (например, #FFFFFF) и не длиннее 7 символов."
        )]
        [Display(Name = "Цвет маркера")]
        public string? Color { get; set; }

        [Range(1, 5, ErrorMessage = "Приоритет сферы должен быть в диапазоне от 1 до 5.")]
        [Display(Name = "Приоритет сферы")]
        public int Priority { get; set; }

        public bool IsActive { get; set; } = true;

        public User User { get; set; } = null!;

        public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    }
}
