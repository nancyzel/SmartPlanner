using System.ComponentModel.DataAnnotations;
using SmartPlanner.Models.Enums;

namespace SmartPlanner.Models
{
    public class Event : Activity
    {
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public long? LocationId { get; set; }

        public bool IsRecurring { get; set; }

        public RecurrenceType? RecurrenceType { get; set; }

        public DateTime? RecurrenceEndDate { get; set; }

        public Location? Location { get; set; }
    }
}
