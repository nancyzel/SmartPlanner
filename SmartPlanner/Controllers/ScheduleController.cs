using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPlanner.Models;
using SmartPlanner.Models.Enums;
using SmartPlanner.Services;

namespace SmartPlanner.Controllers
{
    [Authorize]
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ScheduleService _scheduleService;

        public ScheduleController(ApplicationDbContext context, ScheduleService scheduleService)
        {
            _context = context;
            _scheduleService = scheduleService;
        }

        private long CurrentUserId => long.Parse(User.FindFirstValue("UserId")!);

        // Главная страница календаря
        public IActionResult Index() => View();

        // Метод для запуска генерации расписания кнопкой
        [HttpPost]
        public async Task<IActionResult> Generate(DateTime date)
        {
            await _scheduleService.GenerateDailyScheduleAsync(CurrentUserId, date);
            return Ok();
        }

        [HttpGet]
        public async Task<JsonResult> GetEvents(DateTime start, DateTime end)
        {
            var userId = CurrentUserId;

            var scheduleEntries = await _context
                .ScheduleEntries.Include(s => s.Activity)
                    .ThenInclude(a => a.LifeArea)
                .Where(s => s.UserId == userId && s.StartAt >= start && s.EndAt <= end)
                .ToListAsync();

            var eventsData = scheduleEntries.Select(s => new
            {
                id = s.Id,
                title = GetActivityTitle(s.Activity),
                // ВАЖНО: Добавляем суффикс 'Z', чтобы FullCalendar понял, что это UTC
                start = s.StartAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                end = s.EndAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                backgroundColor = s.Activity?.LifeArea?.Color ?? "#5c6bc0",
                borderColor = "rgba(0,0,0,0.1)",
                allDay = false,
                extendedProps = new
                {
                    description = s.Activity?.Description ?? "Нет описания",
                    sphere = s.Activity?.LifeArea?.Name ?? "Общее",
                    activityType = s.Activity?.Type.ToString().ToLower(),
                },
            });

            return Json(eventsData);
        }

        private string GetActivityTitle(Activity activity)
        {
            if (activity == null)
                return "Без названия";

            string icon = activity.Type == ActivityType.Task ? "📝 " : "📅 ";

            return icon + activity.Title;
        }

        [HttpPost]
        public async Task<IActionResult> UpdateEventTime(long id, DateTime start, DateTime end)
        {
            var userId = CurrentUserId;

            var entry = await _context
                .ScheduleEntries.Include(s => s.Activity)
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (entry == null)
                return NotFound();

            var newStart = DateTime.SpecifyKind(start.ToUniversalTime(), DateTimeKind.Utc);
            var newEnd = DateTime.SpecifyKind(end.ToUniversalTime(), DateTimeKind.Utc);

            entry.StartAt = newStart;
            entry.EndAt = newEnd;
            entry.UpdatedAt = DateTime.UtcNow;
            entry.GenerationSource = GenerationSourceType.Manual;

            if (entry.Activity.Type == ActivityType.Event)
            {
                var sourceEvent = await _context.Events.FirstOrDefaultAsync(e =>
                    e.Id == entry.ActivityId && e.UserId == userId
                );

                if (sourceEvent != null)
                {
                    sourceEvent.StartTime = newStart;
                    sourceEvent.EndTime = newEnd;
                    _context.Events.Update(sourceEvent);
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}
