namespace SmartPlanner.Models
{
    public class DashboardViewModel
    {
        public bool HasPreferences { get; set; }
        public List<TaskItem> UrgentTasks { get; set; } = new();
        public List<Event> TodayEvents { get; set; } = new();
        public int CompletedTasksToday { get; set; }
        public int TotalTasksToday { get; set; }

        public int ProgressPercentage =>
            TotalTasksToday == 0 ? 0 : (CompletedTasksToday * 100) / TotalTasksToday;
    }
}
