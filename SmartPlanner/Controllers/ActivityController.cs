using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPlanner.Models;

namespace SmartPlanner.Controllers
{
    [Authorize]
    public class ActivityController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ActivityController(ApplicationDbContext context)
        {
            _context = context;
        }

        private long CurrentUserId => long.Parse(User.FindFirstValue("UserId")!);

        public async Task<IActionResult> Index()
        {
            var logs = await _context
                .ActivityLogs.Where(l => l.UserId == CurrentUserId)
                .OrderByDescending(l => l.Timestamp)
                .Take(100)
                .ToListAsync();

            return View(logs);
        }
    }
}
