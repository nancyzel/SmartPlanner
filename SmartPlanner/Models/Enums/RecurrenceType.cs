using System.ComponentModel.DataAnnotations;

namespace SmartPlanner.Models.Enums
{
    public enum RecurrenceType
    {
        [Display(Name = "Ежедневно")]
        Daily = 0,

        [Display(Name = "Еженедельно")]
        Weekly = 1,

        [Display(Name = "Ежемесячно")]
        Monthly = 2,

        [Display(Name = "Ежегодно")]
        Annually = 3,
    }
}
