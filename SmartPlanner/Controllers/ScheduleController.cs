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

        // Данные для FullCalendar (вызывается самим календарем через JS)
        [HttpGet]
        public async Task<JsonResult> GetEvents(DateTime start, DateTime end)
        {
            var userId = CurrentUserId;

            var scheduleEntries = await _context
                .ScheduleEntries.Include(s => s.Activity)
                    .ThenInclude(a => a.LifeArea) // Важно: подгружаем сферу!
                .Where(s => s.UserId == userId && s.StartAt >= start && s.EndAt <= end)
                .ToListAsync();

            var eventsData = scheduleEntries.Select(s => new
            {
                id = s.Id,
                title = GetActivityTitle(s.Activity),
                start = s.StartAt.ToString("s"),
                end = s.EndAt.ToString("s"),
                backgroundColor = s.Activity?.LifeArea?.Color ?? "#5c6bc0",
                borderColor = "rgba(0,0,0,0.1)",
                allDay = false,
                // Добавляем тип активности для CSS классов
                extendedProps = new
                {
                    description = s.Activity?.Description ?? "Нет описания",
                    sphere = s.Activity?.LifeArea?.Name ?? "Общее",
                    activityType = s.Activity?.Type.ToString().ToLower(), // "task" или "event"
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
    }
}
