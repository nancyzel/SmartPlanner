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
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LogService _log;

        public TasksController(ApplicationDbContext context, LogService log)
        {
            _context = context;
            _log = log;
        }

        private long CurrentUserId => long.Parse(User.FindFirstValue("UserId")!);

        public async Task<IActionResult> Index()
        {
            var tasks = await _context
                .Tasks.Include(t => t.LifeArea)
                .Where(t => t.UserId == CurrentUserId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return View(tasks);
        }

        public IActionResult Create()
        {
            ViewBag.LifeAreas = new SelectList(
                _context.LifeAreas.Where(u => u.UserId == CurrentUserId),
                "Id",
                "Name"
            );
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TaskItem task)
        {
            ModelState.Remove(nameof(task.User));
            ModelState.Remove(nameof(task.LifeArea));

            if (ModelState.IsValid)
            {
                task.UserId = CurrentUserId;
                task.Type = ActivityType.Task;
                task.CreatedAt = DateTime.UtcNow;
                task.UpdatedAt = DateTime.UtcNow;
                task.Deadline = task.Deadline?.ToUniversalTime();

                _context.Tasks.Add(task);
                await _context.SaveChangesAsync();
                await _log.LogAsync(ActionType.Create, "Задача", task.Id);
                return RedirectToAction(nameof(Index));
            }
            ViewBag.LifeAreas = new SelectList(
                _context.LifeAreas.Where(u => u.UserId == CurrentUserId),
                "Id",
                "Name"
            );
            return View(task);
        }

        public async Task<IActionResult> Edit(long id)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t =>
                t.Id == id && t.UserId == CurrentUserId
            );

            if (task == null)
                return NotFound();

            ViewBag.LifeAreas = new SelectList(
                _context.LifeAreas.Where(u => u.UserId == CurrentUserId),
                "Id",
                "Name"
            );
            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, TaskItem task)
        {
            if (id != task.Id)
                return BadRequest();

            var existingTask = await _context.Tasks.FirstOrDefaultAsync(t =>
                t.Id == id && t.UserId == CurrentUserId
            );
            if (existingTask == null)
                return NotFound();

            ModelState.Remove(nameof(task.User));
            ModelState.Remove(nameof(task.LifeArea));

            if (ModelState.IsValid)
            {
                existingTask.Type = ActivityType.Task;
                existingTask.Title = task.Title;
                existingTask.Description = task.Description;
                existingTask.LifeAreaId = task.LifeAreaId;
                existingTask.EnergyRequired = task.EnergyRequired;
                existingTask.UpdatedAt = DateTime.UtcNow;

                existingTask.Deadline = task.Deadline?.ToUniversalTime();
                existingTask.Status = task.Status;
                existingTask.ProgressPercent = task.ProgressPercent;
                existingTask.Priority = task.Priority;
                existingTask.EstimatedDuration = task.EstimatedDuration;
                existingTask.ActualDuration = task.ActualDuration;
                existingTask.IsFlexible = task.IsFlexible;
                try
                {
                    await _context.SaveChangesAsync();
                    await _log.LogAsync(ActionType.Update, "Задача", task.Id);
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
            ViewBag.LifeAreas = new SelectList(
                _context.LifeAreas.Where(u => u.UserId == CurrentUserId),
                "Id",
                "Name"
            );
            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t =>
                t.Id == id && t.UserId == CurrentUserId
            );

            if (task != null)
            {
                _context.Tasks.Remove(task);
                await _context.SaveChangesAsync();
                await _log.LogAsync(ActionType.Delete, "Задача", task.Id);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
