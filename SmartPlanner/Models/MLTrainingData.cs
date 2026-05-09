using System.ComponentModel.DataAnnotations;

namespace SmartPlanner.Models
{
    public class MLTrainingData
    {
        public long Id { get; set; }

        [Required]
        public long TaskItemId { get; set; }

        [Range(1, 1440)]
        public int PredictedDuration { get; set; }

        [Range(1, 1440)]
        public int ActualDuration { get; set; }

        public decimal PredictionError { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public TaskItem TaskItem { get; set; } = null!;
    }
}