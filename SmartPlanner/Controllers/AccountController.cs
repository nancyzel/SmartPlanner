using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPlanner.Models;
using SmartPlanner.Services;

namespace SmartPlanner.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthService _auth;

        public AccountController(ApplicationDbContext context, AuthService auth)
        {
            _context = context;
            _auth = auth;
        }

        // --- РЕГИСТРАЦИЯ ---

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(User user, string password)
        {
            ModelState.Remove(nameof(user.PasswordHash));
            ModelState.Remove(nameof(user.Salt));
            ModelState.Remove(nameof(user.Activities));
            ModelState.Remove(nameof(user.LifeAreas));
            ModelState.Remove(nameof(user.Locations));
            ModelState.Remove(nameof(user.ScheduleEntries));
            ModelState.Remove(nameof(user.ActivityLogs));

            if (ModelState.IsValid)
            {
                if (await _context.Users.AnyAsync(u => u.Email == user.Email))
                {
                    ModelState.AddModelError("Email", "Пользователь с таким Email уже существует");
                    return View(user);
                }

                var (hash, salt) = _auth.HashPassword(password);

                user.PasswordHash = hash;
                user.Salt = salt;
                user.CreatedAt = DateTime.UtcNow;

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction("Login");
            }
            return View(user);
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user != null && _auth.Verify(password, user.PasswordHash, user.Salt))
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("UserId", user.Id.ToString()),
                };

                var identity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme
                );
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity)
                );

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Неверный логин или пароль");
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }
    }
}
