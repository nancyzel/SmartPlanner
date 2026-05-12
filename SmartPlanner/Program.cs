using Microsoft.EntityFrameworkCore;
using SmartPlanner.Models;
using SmartPlanner.Services;

namespace SmartPlanner
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();

            // Настройка БД
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
            );

            // Аутентификация
            builder
                .Services.AddAuthentication("Cookies")
                .AddCookie(
                    "Cookies",
                    options =>
                    {
                        options.LoginPath = "/Account/Login";
                        options.AccessDeniedPath = "/Account/AccessDenied";
                        options.Cookie.Name = "SmartPlannerAuth"; // Хорошим тоном считается дать имя куки
                    }
                );

            // Регистрация твоих сервисов
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<ScheduleService>();
            builder.Services.AddScoped<LogService>();
            builder.Services.AddHttpContextAccessor();

            var app = builder.Build();

            // --- НОВЫЙ БЛОК: Автоматическая миграция БД ---
            // Это позволит базе в Neon обновляться сразу при запуске сайта
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<ApplicationDbContext>();
                    context.Database.Migrate();
                }
                catch (Exception ex)
                {
                    // Логируем ошибку, если база недоступна
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Ошибка при применении миграций БД.");
                }
            }
            // ----------------------------------------------

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            // Если .MapStaticAssets() не подгружает стили, добавь сюда app.UseStaticFiles();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}"
                )
                .WithStaticAssets();

            app.Run();
        }
    }
}
