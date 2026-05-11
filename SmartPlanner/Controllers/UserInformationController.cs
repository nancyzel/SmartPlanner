using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPlanner.Models;
using SmartPlanner.Services;

namespace SmartPlanner.Controllers
{
    [Authorize]
    public class UserInformationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthService _auth;

        public UserInformationController(ApplicationDbContext context, AuthService auth)
        {
            _context = context;
            _auth = auth;
        }

        // Используем тот же ключ "UserId", что и в других контроллерах
        private long CurrentUserId => long.Parse(User.FindFirstValue("UserId")!);

        public async Task<IActionResult> Index()
        {
            var user = await _context
                .Users.Include(u => u.UserPreference)
                .FirstOrDefaultAsync(u => u.Id == CurrentUserId);

            if (user == null)
                return NotFound();

            // Если у пользователя еще нет настроек в БД, создаем объект в памяти,
            // чтобы View могла отобразить пустые поля или значения по умолчанию
            if (user.UserPreference == null)
            {
                user.UserPreference = new UserPreference
                {
                    UserId = CurrentUserId,
                    WorkStartTime = new TimeSpan(9, 0, 0), // Можно задать дефолт 09:00
                    WorkEndTime = new TimeSpan(18, 0, 0), // и 18:00
                    PreferredBreakInterval = 60,
                };
            }

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(User model, string? newPassword)
        {
            var userId = CurrentUserId;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound();

            // Обновляем базовые поля
            user.FullName = model.FullName;
            user.ShortName = model.ShortName;

            // Если введен новый пароль — хешируем его
            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                var (hash, salt) = _auth.HashPassword(newPassword);
                user.PasswordHash = hash;
                user.Salt = salt;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Данные профиля обновлены!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpsertPreference(UserPreference preference)
        {
            var userId = CurrentUserId;
            var existingPref = await _context.UserPreferences.FirstOrDefaultAsync(p =>
                p.UserId == userId
            );

            if (existingPref == null)
            {
                preference.UserId = userId;
                _context.UserPreferences.Add(preference);
            }
            else
            {
                // Маппинг всех полей для алгоритма
                existingPref.WorkStartTime = preference.WorkStartTime;
                existingPref.WorkEndTime = preference.WorkEndTime;
                existingPref.PeakProductivityStart = preference.PeakProductivityStart;
                existingPref.PeakProductivityEnd = preference.PeakProductivityEnd;
                existingPref.MaxTasksPerDay = preference.MaxTasksPerDay;
                existingPref.BreakDuration = preference.BreakDuration;
                existingPref.PreferredBreakInterval = preference.PreferredBreakInterval;

                _context.UserPreferences.Update(existingPref);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Настройки планирования сохранены!";
            return RedirectToAction(nameof(Index));
        }
    }
}
