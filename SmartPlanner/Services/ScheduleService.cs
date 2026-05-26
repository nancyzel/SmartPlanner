using Microsoft.EntityFrameworkCore;
using SmartPlanner.Models;
using SmartPlanner.Models.Enums;

namespace SmartPlanner.Services
{
    public class ScheduleService
    {
        private readonly ApplicationDbContext _context;

        // Коэффициенты для весов (можно вынести в настройки приложения)
        private const double PriorityWeight = 0.5;
        private const double UrgencyWeight = 0.5;

        public ScheduleService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task GenerateDailyScheduleAsync(long userId, DateTime date)
        {
            // 0. Получаем часовой пояс пользователя (берём из БД или ставим дефолт UTC+5)
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            var userZoneStr = user?.Timezone ?? "Asia/Yekaterinburg";
            var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(userZoneStr);

            // 1. Получаем предпочтения пользователя
            var prefs = await _context.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);

            var workStart = prefs?.WorkStartTime ?? TimeSpan.FromHours(8);
            var workEnd = prefs?.WorkEndTime ?? TimeSpan.FromHours(22);

            // ВАЖНО: Формируем границы рабочего дня в ЛОКАЛЬНОМ времени пользователя
            var localDate = date.Date; // Например, 26.05.2026 00:00:00
            var localDayStart = localDate.Add(workStart); // 26.05.2026 08:00:00
            var localDayEnd = localDate.Add(workEnd); // 26.05.2026 22:00:00

            // Переводим границы в UTC для работы с базой данных
            DateTime dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(localDayStart, userTimeZone);
            DateTime dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(localDayEnd, userTimeZone);

            int maxTasks = prefs?.MaxTasksPerDay ?? 10;
            int breakDuration = prefs?.BreakDuration ?? 15;
            int breakInterval = prefs?.PreferredBreakInterval ?? 120;

            // Вычисляем полные локальные сутки пользователя в формате UTC для фильтрации жестких событий
            DateTime startOfLocalDayUtc = TimeZoneInfo.ConvertTimeToUtc(localDate, userTimeZone);
            DateTime endOfLocalDayUtc = TimeZoneInfo.ConvertTimeToUtc(
                localDate.AddDays(1),
                userTimeZone
            );

            // 2. Получаем жесткие события (Events), попадающие в локальные сутки пользователя
            var events = await _context
                .Events.Where(e =>
                    e.UserId == userId
                    && e.StartTime >= startOfLocalDayUtc
                    && e.StartTime < endOfLocalDayUtc
                )
                .OrderBy(e => e.StartTime)
                .ToListAsync();

            // 3. Получаем список невыполненных задач (Tasks)
            var tasks = await _context
                .Tasks.Where(t => t.UserId == userId && t.Status != TaskStatusType.Done)
                .ToListAsync();

            // 4. Расчет весов (передаем localDate для корректного расчета дней до дедлайна)
            var weightedTasks = tasks
                .Select(t => new { Task = t, BaseWeight = CalculateBaseWeight(t, localDate) })
                .ToList();

            // 5. Очищаем старое расписание именно за эти сутки
            var oldEntries = _context.ScheduleEntries.Where(s =>
                s.UserId == userId
                && s.StartAt >= startOfLocalDayUtc
                && s.StartAt < endOfLocalDayUtc
            );
            _context.ScheduleEntries.RemoveRange(oldEntries);

            // 6. Создаем "занятые" слоты из Events
            var schedule = new List<ScheduleEntry>();
            foreach (var ev in events)
            {
                schedule.Add(
                    new ScheduleEntry
                    {
                        UserId = userId,
                        ActivityId = ev.Id,
                        StartAt = ev.StartTime, // Они уже сохранены в UTC
                        EndAt = ev.EndTime,
                        Status = ScheduleEntryStatus.Planned,
                        GenerationSource = GenerationSourceType.Manual,
                    }
                );
            }

            // 7. Алгоритм заполнения "окон" (работаем в UTC по указателю currentPointer)
            DateTime currentPointer = dayStartUtc;

            var overlappingEvent = schedule
                .Where(s => s.StartAt <= dayStartUtc && s.EndAt > dayStartUtc)
                .OrderByDescending(s => s.EndAt)
                .FirstOrDefault();

            if (overlappingEvent != null)
            {
                currentPointer = overlappingEvent.EndAt;
            }

            long? lastLifeAreaId = null;
            int tasksScheduledToday = 0;
            int continuousWorkMinutes = 0;

            while (
                currentPointer < dayEndUtc && weightedTasks.Any() && tasksScheduledToday < maxTasks
            )
            {
                var activeOrNextSlot = schedule
                    .Where(s => s.EndAt > currentPointer)
                    .OrderBy(s => s.StartAt)
                    .FirstOrDefault();

                if (activeOrNextSlot != null && activeOrNextSlot.StartAt <= currentPointer)
                {
                    currentPointer = activeOrNextSlot.EndAt;
                    continuousWorkMinutes = 0;
                    continue;
                }

                DateTime nextBusyTime = activeOrNextSlot?.StartAt ?? dayEndUtc;
                int gapMinutes = (int)(nextBusyTime - currentPointer).TotalMinutes;

                if (gapMinutes < 15)
                {
                    if (activeOrNextSlot != null)
                    {
                        currentPointer = activeOrNextSlot.EndAt;
                        continuousWorkMinutes = 0;
                        continue;
                    }
                }

                if (continuousWorkMinutes >= breakInterval)
                {
                    if (gapMinutes >= breakDuration)
                    {
                        currentPointer = currentPointer.AddMinutes(breakDuration);
                        continuousWorkMinutes = 0;
                        continue;
                    }
                    else
                    {
                        currentPointer = nextBusyTime;
                        continuousWorkMinutes = 0;
                        continue;
                    }
                }

                if (gapMinutes >= 15)
                {
                    // ДЛЯ УЧЕТА БИОРИТМОВ переводим UTC-указатель обратно в ЛОКАЛЬНОЕ время пользователя
                    DateTime localCurrentPointer = TimeZoneInfo.ConvertTimeFromUtc(
                        currentPointer,
                        userTimeZone
                    );
                    var currentTimeOfDay = localCurrentPointer.TimeOfDay;

                    bool isPeakTime =
                        prefs != null
                        && prefs.PeakProductivityStart.HasValue
                        && prefs.PeakProductivityEnd.HasValue
                        && currentTimeOfDay >= prefs.PeakProductivityStart.Value
                        && currentTimeOfDay <= prefs.PeakProductivityEnd.Value;

                    var bestTaskMatch = weightedTasks
                        .Where(t => t.Task.EstimatedDuration <= gapMinutes)
                        .OrderByDescending(t =>
                        {
                            double dynamicScore = t.BaseWeight;
                            if (t.Task.LifeAreaId != lastLifeAreaId)
                                dynamicScore += 10;
                            if (isPeakTime && t.Task.EnergyRequired >= 4)
                                dynamicScore += 15;
                            else if (!isPeakTime && t.Task.EnergyRequired <= 2)
                                dynamicScore += 5;
                            return dynamicScore;
                        })
                        .FirstOrDefault();

                    if (bestTaskMatch != null)
                    {
                        var task = bestTaskMatch.Task;

                        schedule.Add(
                            new ScheduleEntry
                            {
                                UserId = userId,
                                ActivityId = task.Id,
                                StartAt = DateTime.SpecifyKind(currentPointer, DateTimeKind.Utc),
                                EndAt = DateTime.SpecifyKind(
                                    currentPointer.AddMinutes(task.EstimatedDuration),
                                    DateTimeKind.Utc
                                ),
                                Status = ScheduleEntryStatus.Planned,
                                GenerationSource = GenerationSourceType.Automatic,
                                ConfidenceScore = 0.8m,
                            }
                        );

                        currentPointer = currentPointer.AddMinutes(task.EstimatedDuration + 5);
                        continuousWorkMinutes += task.EstimatedDuration;
                        lastLifeAreaId = task.LifeAreaId;
                        tasksScheduledToday++;

                        weightedTasks.Remove(bestTaskMatch);
                        continue;
                    }
                }

                var nextEvent = schedule
                    .Where(s => s.StartAt >= currentPointer)
                    .OrderBy(s => s.StartAt)
                    .FirstOrDefault();
                if (nextEvent != null)
                {
                    currentPointer = nextEvent.EndAt;
                    continuousWorkMinutes = 0;
                }
                else
                {
                    break;
                }
            }

            await _context.ScheduleEntries.AddRangeAsync(schedule);
            await _context.SaveChangesAsync();
        }

        private double CalculateBaseWeight(TaskItem task, DateTime targetDate)
        {
            // вес по приоритету задачи
            double weight = task.Priority * PriorityWeight;

            if (task.Deadline.HasValue)
            {
                double daysUntilDeadline = (task.Deadline.Value - targetDate).TotalDays;

                if (daysUntilDeadline <= 0)
                    // если задача просрочена, к ней повышенное внимание
                    weight += 15;
                else
                    // вес по срочности задачи
                    weight += (1.0 / daysUntilDeadline) * UrgencyWeight * 10;
            }

            return weight;
        }
    }
}
