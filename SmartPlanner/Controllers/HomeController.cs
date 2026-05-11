using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPlanner.Models;
using SmartPlanner.Models.Enums;

namespace SmartPlanner.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        private long CurrentUserId => long.Parse(User.FindFirstValue("UserId")!);

        public async Task<IActionResult> Index()
        {
            var userId = CurrentUserId;
            var today = DateTime.UtcNow.Date;
            var scheduledTaskIds = await _context
                .ScheduleEntries.Where(s => s.UserId == userId && s.StartAt.Date == today)
                .Select(s => s.ActivityId)
                .ToListAsync();

            var viewModel = new DashboardViewModel
            {
                // 1. Проверка наличия настроек (предположим модель называется UserInformation или UserPreference)
                HasPreferences = await _context.UserPreferences.AnyAsync(p => p.UserId == userId),

                // 2. Топ-3 приоритетных задачи, которые еще не выполнены
                UrgentTasks = await _context
                    .Tasks.Where(t => t.UserId == userId && t.Status != TaskStatusType.Done)
                    .OrderBy(t => t.Priority)
                    .Take(3)
                    .ToListAsync(),

                // 3. Мероприятия на сегодня
                TodayEvents = await _context
                    .Events.Where(e => e.UserId == userId && e.StartTime.Date == today)
                    .OrderBy(e => e.StartTime)
                    .ToListAsync(),

                // 4. Статистика для прогресс-бара (задачи на сегодня)
                TotalTasksToday = await _context.Tasks.CountAsync(t =>
                    scheduledTaskIds.Contains(t.Id)
                ),

                CompletedTasksToday = await _context.Tasks.CountAsync(t =>
                    scheduledTaskIds.Contains(t.Id) && t.Status == TaskStatusType.Done
                ),
            };

            return View(viewModel);
        }
    }
}
