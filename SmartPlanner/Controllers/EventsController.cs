using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartPlanner.Models;
using SmartPlanner.Models.Enums;
using SmartPlanner.Services;

namespace SmartPlanner.Controllers
{
    [Authorize]
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LogService _log;

        public EventsController(ApplicationDbContext context, LogService log)
        {
            _context = context;
            _log = log;
        }

        private long CurrentUserId => long.Parse(User.FindFirstValue("UserId")!);

        private async Task<TimeZoneInfo> GetUserTimeZoneAsync()
        {
            var tzString = await _context
                .Users.Where(u => u.Id == CurrentUserId)
                .Select(u => u.Timezone)
                .FirstOrDefaultAsync();

            return TimeZoneInfo.FindSystemTimeZoneById(tzString ?? "Asia/Yekaterinburg");
        }

        public async Task<IActionResult> Index()
        {
            var events = await _context
                .Events.Include(e => e.LifeArea)
                .Include(e => e.Location)
                .Where(e => e.UserId == CurrentUserId)
                .OrderBy(e => e.StartTime)
                .ToListAsync();
            return View(events);
        }

        public IActionResult Create()
        {
            PopulateLists();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Event @event)
        {
            ModelState.Remove(nameof(@event.User));
            ModelState.Remove(nameof(@event.LifeArea));
            ModelState.Remove(nameof(@event.Location));

            if (ModelState.IsValid)
            {
                var userTimeZone = await GetUserTimeZoneAsync();

                @event.UserId = CurrentUserId;
                @event.Type = ActivityType.Event;
                @event.CreatedAt = DateTime.UtcNow;
                @event.UpdatedAt = DateTime.UtcNow;

                // Из формы пришло Unspecified время пользователя. Переводим его в UTC.
                @event.StartTime = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(@event.StartTime, DateTimeKind.Unspecified),
                    userTimeZone
                );
                @event.EndTime = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(@event.EndTime, DateTimeKind.Unspecified),
                    userTimeZone
                );

                if (@event.RecurrenceEndDate.HasValue)
                    @event.RecurrenceEndDate = TimeZoneInfo.ConvertTimeToUtc(
                        DateTime.SpecifyKind(
                            @event.RecurrenceEndDate.Value,
                            DateTimeKind.Unspecified
                        ),
                        userTimeZone
                    );

                _context.Events.Add(@event);
                await _context.SaveChangesAsync();
                await _log.LogAsync(ActionType.Create, "Мероприятие", @event.Id);
                return RedirectToAction(nameof(Index));
            }
            PopulateLists();
            return View(@event);
        }

        public async Task<IActionResult> Edit(long id)
        {
            var @event = await _context.Events.FirstOrDefaultAsync(e =>
                e.Id == id && e.UserId == CurrentUserId
            );

            if (@event == null)
                return NotFound();

            // Конвертируем UTC из базы данных в локальное время пользователя для отображения в форме редактирования
            var userTimeZone = await GetUserTimeZoneAsync();
            @event.StartTime = TimeZoneInfo.ConvertTimeFromUtc(@event.StartTime, userTimeZone);
            @event.EndTime = TimeZoneInfo.ConvertTimeFromUtc(@event.EndTime, userTimeZone);

            if (@event.RecurrenceEndDate.HasValue)
                @event.RecurrenceEndDate = TimeZoneInfo.ConvertTimeFromUtc(
                    @event.RecurrenceEndDate.Value,
                    userTimeZone
                );

            PopulateLists();
            return View(@event);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, Event @event)
        {
            if (id != @event.Id)
                return BadRequest();

            var existingEvent = await _context.Events.FirstOrDefaultAsync(e =>
                e.Id == id && e.UserId == CurrentUserId
            );

            if (existingEvent == null)
                return NotFound();

            ModelState.Remove(nameof(@event.User));
            ModelState.Remove(nameof(@event.LifeArea));
            ModelState.Remove(nameof(@event.Location));

            if (ModelState.IsValid)
            {
                var userTimeZone = await GetUserTimeZoneAsync();

                existingEvent.Title = @event.Title;
                existingEvent.Description = @event.Description;
                existingEvent.LifeAreaId = @event.LifeAreaId;
                existingEvent.LocationId = @event.LocationId;
                existingEvent.IsRecurring = @event.IsRecurring;
                existingEvent.RecurrenceType = @event.RecurrenceType;
                existingEvent.EnergyRequired = @event.EnergyRequired;
                existingEvent.UpdatedAt = DateTime.UtcNow;

                // Конвертируем измененное локальное время формы обратно в UTC для базы данных
                existingEvent.StartTime = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(@event.StartTime, DateTimeKind.Unspecified),
                    userTimeZone
                );
                existingEvent.EndTime = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(@event.EndTime, DateTimeKind.Unspecified),
                    userTimeZone
                );

                if (@event.RecurrenceEndDate.HasValue)
                    existingEvent.RecurrenceEndDate = TimeZoneInfo.ConvertTimeToUtc(
                        DateTime.SpecifyKind(
                            @event.RecurrenceEndDate.Value,
                            DateTimeKind.Unspecified
                        ),
                        userTimeZone
                    );
                else
                    existingEvent.RecurrenceEndDate = null;

                try
                {
                    await _context.SaveChangesAsync();
                    await _log.LogAsync(ActionType.Update, "Мероприятие", @event.Id);
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(
                        "",
                        "Не удалось сохранить изменения. Попробуйте еще раз."
                    );
                }
            }
            PopulateLists();
            return View(@event);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            var @event = await _context.Events.FirstOrDefaultAsync(e =>
                e.Id == id && e.UserId == CurrentUserId
            );
            if (@event != null)
            {
                _context.Events.Remove(@event);
                await _context.SaveChangesAsync();
                await _log.LogAsync(ActionType.Create, "Мероприятие", @event.Id);
            }
            return RedirectToAction(nameof(Index));
        }

        private void PopulateLists()
        {
            ViewBag.LifeAreas = new SelectList(
                _context.LifeAreas.Where(u => u.UserId == CurrentUserId),
                "Id",
                "Name"
            );
            ViewBag.Locations = new SelectList(
                _context.Locations.Where(u => u.UserId == CurrentUserId),
                "Id",
                "Name"
            );
        }
    }
}
