using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SmartPlanner.Models
{
    public class User
    {
        public long Id { get; set; }

        [Required(ErrorMessage = "Пожалуйста, введите ваше полное имя.")]
        [StringLength(100, ErrorMessage = "Имя не может превышать 100 символов.")]
        [Display(Name = "Полное имя")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(30, ErrorMessage = "Короткое имя не может превышать 30 символов.")]
        public string? ShortName { get; set; }

        [Required(ErrorMessage = "Пожалуйста, укажите адрес электронной почты.")]
        [EmailAddress(ErrorMessage = "Введен некорректный формат Email.")]
        [Display(Name = "Электронная почта")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Пароль обязателен для заполнения.")]
        [Display(Name = "Пароль")]
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
