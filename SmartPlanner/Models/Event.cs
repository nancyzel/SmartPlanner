using System.ComponentModel.DataAnnotations;
using SmartPlanner.Models.Enums;

namespace SmartPlanner.Models
{
    public class Event : Activity
    {
        [Required(ErrorMessage = "Укажите время начала мероприятия.")]
        [Display(Name = "Время начала")]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "Укажите время окончания мероприятия.")]
        [Display(Name = "Время окончания")]
        public DateTime EndTime { get; set; }

        public long? LocationId { get; set; }

        public bool IsRecurring { get; set; }

        public RecurrenceType? RecurrenceType { get; set; }

        public DateTime? RecurrenceEndDate { get; set; }

        public Location? Location { get; set; }
    }
}
