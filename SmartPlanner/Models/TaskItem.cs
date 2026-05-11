using System.ComponentModel.DataAnnotations;
using SmartPlanner.Models.Enums;

namespace SmartPlanner.Models
{
    public class TaskItem : Activity
    {
        public DateTime? Deadline { get; set; }

        [Range(0, 100)]
        public int ProgressPercent { get; set; }

        [Range(1, 1440)]
        public int EstimatedDuration { get; set; }

        [Range(1, 1440)]
        public int? ActualDuration { get; set; }

        [Range(1, 5)]
        public int Priority { get; set; }

        public bool IsFlexible { get; set; } = true;

        public TaskStatusType Status { get; set; } = TaskStatusType.Planned;

        public ICollection<MLTrainingData> MLTrainingData { get; set; } =
            new List<MLTrainingData>();
    }
}
