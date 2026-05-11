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
    public class LocationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LogService _log;

        public LocationsController(ApplicationDbContext context, LogService log)
        {
            _context = context;
            _log = log;
        }

        private long CurrentUserId => long.Parse(User.FindFirstValue("UserId")!);

        public async Task<IActionResult> Index()
        {
            var locations = await _context
                .Locations.Where(l => l.UserId == CurrentUserId)
                .OrderBy(l => l.Name)
                .ToListAsync();
            return View(locations);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Location location)
        {
            ModelState.Remove(nameof(location.User));

            if (ModelState.IsValid)
            {
                location.UserId = CurrentUserId;
                _context.Locations.Add(location);
                await _context.SaveChangesAsync();
                await _log.LogAsync(ActionType.Create, "Локация", location.Id);
                return RedirectToAction(nameof(Index));
            }
            return View(location);
        }

        public async Task<IActionResult> Edit(long id)
        {
            var location = await _context.Locations.FirstOrDefaultAsync(l =>
                l.Id == id && l.UserId == CurrentUserId
            );

            if (location == null)
                return NotFound();

            return View(location);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, Location location)
        {
            if (id != location.Id)
                return BadRequest();

            var existingLocation = await _context.Locations.FirstOrDefaultAsync(l =>
                l.Id == id && l.UserId == CurrentUserId
            );

            if (existingLocation == null)
                return NotFound();

            ModelState.Remove(nameof(location.User));

            if (ModelState.IsValid)
            {
                existingLocation.Name = location.Name;
                existingLocation.Address = location.Address;
                existingLocation.OpeningTime = location.OpeningTime;
                existingLocation.ClosingTime = location.ClosingTime;

                try
                {
                    await _context.SaveChangesAsync();
                    await _log.LogAsync(ActionType.Update, "Локация", location.Id);
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Не удалось сохранить изменения.");
                }
            }
            return View(location);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            var location = await _context.Locations.FirstOrDefaultAsync(l =>
                l.Id == id && l.UserId == CurrentUserId
            );

            if (location != null)
            {
                _context.Locations.Remove(location);
                await _context.SaveChangesAsync();
                await _log.LogAsync(ActionType.Delete, "Локация", location.Id);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
