using System.Security.Claims;
using SmartPlanner.Models;
using SmartPlanner.Models.Enums;

namespace SmartPlanner.Services
{
    public class LogService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _accessor;

        public LogService(ApplicationDbContext context, IHttpContextAccessor accessor)
        {
            _context = context;
            _accessor = accessor;
        }

        public void Log(ActionType action, string entityType, long entityId)
        {
            var userIdClaim = _accessor.HttpContext?.User.FindFirstValue("UserId");
            if (userIdClaim == null)
                return;

            var log = new ActivityLog
            {
                UserId = long.Parse(userIdClaim),
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Timestamp = DateTime.UtcNow,
            };

            _context.ActivityLogs.Add(log);
            // Используем синхронный или асинхронный метод в зависимости от контекста
            _context.SaveChanges();
        }

        // Асинхронная версия для использования в контроллерах
        public async Task LogAsync(ActionType action, string entityType, long entityId)
        {
            var userIdClaim = _accessor.HttpContext?.User.FindFirstValue("UserId");
            if (userIdClaim == null)
                return;

            var log = new ActivityLog
            {
                UserId = long.Parse(userIdClaim),
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Timestamp = DateTime.UtcNow,
            };

            _context.ActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
