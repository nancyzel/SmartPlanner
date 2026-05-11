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
    public class LifeAreasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LogService _log;

        public LifeAreasController(ApplicationDbContext context, LogService log)
        {
            _context = context;
            _log = log;
        }

        private long CurrentUserId => long.Parse(User.FindFirstValue("UserId")!);

        public async Task<IActionResult> Index()
        {
            var areas = await _context
                .LifeAreas.Where(a => a.UserId == CurrentUserId)
                .OrderByDescending(a => a.Priority)
                .ToListAsync();
            return View(areas);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LifeArea area)
        {
            ModelState.Remove(nameof(area.User));

            if (ModelState.IsValid)
            {
                area.UserId = CurrentUserId;
                _context.LifeAreas.Add(area);
                await _context.SaveChangesAsync();
                await _log.LogAsync(ActionType.Create, "Сфера жизни", area.Id);
                return RedirectToAction(nameof(Index));
            }
            return View(area);
        }

        public async Task<IActionResult> Edit(long id)
        {
            var area = await _context.LifeAreas.FirstOrDefaultAsync(a =>
                a.Id == id && a.UserId == CurrentUserId
            );

            if (area == null)
                return NotFound();

            return View(area);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, LifeArea area)
        {
            if (id != area.Id)
                return BadRequest();

            var existingArea = await _context.LifeAreas.FirstOrDefaultAsync(a =>
                a.Id == id && a.UserId == CurrentUserId
            );
            ModelState.Remove(nameof(area.User));

            if (existingArea == null)
                return NotFound();

            if (ModelState.IsValid)
            {
                existingArea.Name = area.Name;
                existingArea.Color = area.Color;
                existingArea.Priority = area.Priority;
                existingArea.IsActive = area.IsActive;

                await _context.SaveChangesAsync();
                await _log.LogAsync(ActionType.Update, "Сфера жизни", area.Id);
                return RedirectToAction(nameof(Index));
            }
            return View(area);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            var area = await _context
                .LifeAreas.Include(a => a.Activities)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == CurrentUserId);

            if (area != null)
            {
                _context.LifeAreas.Remove(area);
                await _context.SaveChangesAsync();
                await _log.LogAsync(ActionType.Delete, "Сфера жизни", area.Id);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
