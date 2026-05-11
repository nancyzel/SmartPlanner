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
                @event.UserId = CurrentUserId;
                @event.Type = ActivityType.Event;
                @event.CreatedAt = DateTime.UtcNow;
                @event.UpdatedAt = DateTime.UtcNow;

                @event.StartTime = @event.StartTime.ToUniversalTime();
                @event.EndTime = @event.EndTime.ToUniversalTime();
                if (@event.RecurrenceEndDate.HasValue)
                    @event.RecurrenceEndDate = @event.RecurrenceEndDate.Value.ToUniversalTime();

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
                existingEvent.Type = ActivityType.Event;
                existingEvent.Title = @event.Title;
                existingEvent.Description = @event.Description;
                existingEvent.LifeAreaId = @event.LifeAreaId;
                existingEvent.LocationId = @event.LocationId;
                existingEvent.StartTime = @event.StartTime.ToUniversalTime();
                existingEvent.EndTime = @event.EndTime.ToUniversalTime();
                existingEvent.IsRecurring = @event.IsRecurring;
                existingEvent.RecurrenceType = @event.RecurrenceType;
                existingEvent.RecurrenceEndDate = @event.RecurrenceEndDate?.ToUniversalTime();
                existingEvent.EnergyRequired = @event.EnergyRequired;
                existingEvent.UpdatedAt = DateTime.UtcNow;

                try
                {
                    await _context.SaveChangesAsync();
                    await _log.LogAsync(ActionType.Create, "Мероприятие", @event.Id);
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
