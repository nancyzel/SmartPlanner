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
        private readonly TaskPredictorService _mlService;

        public TasksController(
            ApplicationDbContext context,
            LogService log,
            TaskPredictorService mlService
        )
        {
            _context = context;
            _log = log;
            _mlService = mlService;
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
        public async Task<IActionResult> Create(TaskItem task, int? OriginalEstimatedDuration)
        {
            ModelState.Remove(nameof(task.User));
            ModelState.Remove(nameof(task.LifeArea));

            if (ModelState.IsValid)
            {
                var userTimeZone = await GetUserTimeZoneAsync();
                task.UserId = CurrentUserId;
                task.Type = ActivityType.Task;
                task.CreatedAt = DateTime.UtcNow;
                task.UpdatedAt = DateTime.UtcNow;

                if (task.Deadline.HasValue)
                    task.Deadline = TimeZoneInfo.ConvertTimeToUtc(
                        DateTime.SpecifyKind(task.Deadline.Value, DateTimeKind.Unspecified),
                        userTimeZone
                    );

                // Если пользователь принял ИИ-совет, изначальная оценка придет из скрытого поля JS.
                // Если не принимал — изначальная оценка равна тому, что сейчас в task.EstimatedDuration.
                int originalDuration = OriginalEstimatedDuration ?? task.EstimatedDuration;

                // Делаем финальный прогноз на основе ИЗНАЧАЛЬНЫХ данных
                int mlPredictedDuration = _mlService.PredictDuration(
                    new TaskItem
                    {
                        Priority = task.Priority,
                        EnergyRequired = task.EnergyRequired,
                        LifeAreaId = task.LifeAreaId,
                        EstimatedDuration = originalDuration,
                    }
                );

                _context.Tasks.Add(task);
                await _context.SaveChangesAsync();

                var mlLog = new MLTrainingData
                {
                    TaskItemId = task.Id,
                    PredictedDuration = mlPredictedDuration,
                    UserOriginalDuration = originalDuration, // Сохраняем в новое поле
                    ActualDuration = task.EstimatedDuration, // Временная заглушка до завершения задачи
                    PredictionError = 0,
                };
                _context.MLTrainingData.Add(mlLog);
                await _context.SaveChangesAsync();

                await _log.LogAsync(ActionType.Create, "Задача", task.Id);

                // Удаляем TempData с сообщением, так как теперь диалог происходит в интерфейсе
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

            if (task.Deadline.HasValue)
            {
                var userTimeZone = await GetUserTimeZoneAsync();
                task.Deadline = TimeZoneInfo.ConvertTimeFromUtc(task.Deadline.Value, userTimeZone);
            }

            ViewBag.LifeAreas = new SelectList(
                _context.LifeAreas.Where(u => u.UserId == CurrentUserId),
                "Id",
                "Name",
                task.LifeAreaId
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
                var userTimeZone = await GetUserTimeZoneAsync();
                bool isJustFinished = (
                    task.Status == TaskStatusType.Done && existingTask.Status != TaskStatusType.Done
                );

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

                if (task.Deadline.HasValue)
                    existingTask.Deadline = TimeZoneInfo.ConvertTimeToUtc(
                        DateTime.SpecifyKind(task.Deadline.Value, DateTimeKind.Unspecified),
                        userTimeZone
                    );
                else
                    existingTask.Deadline = null;
                try
                {
                    await _context.SaveChangesAsync();

                    if (isJustFinished && existingTask.ActualDuration.HasValue)
                    {
                        var mlLog = await _context.MLTrainingData.FirstOrDefaultAsync(m =>
                            m.TaskItemId == existingTask.Id
                        );

                        if (mlLog != null)
                        {
                            mlLog.ActualDuration = existingTask.ActualDuration.Value;
                            mlLog.PredictionError = Math.Abs(
                                mlLog.PredictedDuration - mlLog.ActualDuration
                            );
                            _context.MLTrainingData.Update(mlLog);
                            await _context.SaveChangesAsync();
                        }

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var optionsBuilder =
                                    new DbContextOptionsBuilder<ApplicationDbContext>();
                            }
                            catch
                            {
                                //логгирование ошибок
                            }
                        });

                        await _mlService.TrainModelAsync();
                    }

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
                "Name",
                task.LifeAreaId
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

        // Эндпоинт для AJAX запросов из формы создания
        [HttpPost]
        [IgnoreAntiforgeryToken] // Упрощает вызов из JS
        public IActionResult GetAIPrediction([FromBody] PredictRequestDto request)
        {
            var dummyTask = new TaskItem
            {
                Priority = request.Priority,
                EnergyRequired = request.EnergyRequired,
                LifeAreaId = request.LifeAreaId,
                EstimatedDuration = request.EstimatedDuration,
            };

            int predicted = _mlService.PredictDuration(dummyTask);
            return Json(new { predictedDuration = predicted });
        }
    }

    public class PredictRequestDto
    {
        public int Priority { get; set; }
        public int EnergyRequired { get; set; }
        public long? LifeAreaId { get; set; }
        public int EstimatedDuration { get; set; }
    }
}
