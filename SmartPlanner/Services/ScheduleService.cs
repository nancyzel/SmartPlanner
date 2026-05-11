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
            DateTime utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            // 1. Получаем предпочтения пользователя (или используем значения по умолчанию)
            var prefs = await _context.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);

            var workStart = prefs?.WorkStartTime ?? TimeSpan.FromHours(8);
            var workEnd = prefs?.WorkEndTime ?? TimeSpan.FromHours(22);
            var dayStart = utcDate.Date.Add(workStart);
            var dayEnd = utcDate.Date.Add(workEnd);

            int maxTasks = prefs?.MaxTasksPerDay ?? 10;
            int breakDuration = prefs?.BreakDuration ?? 15;
            int breakInterval = prefs?.PreferredBreakInterval ?? 120; // Например, каждые 2 часа

            // 2. Получаем жесткие события (Events)
            var events = await _context
                .Events.Where(e => e.UserId == userId && e.StartTime.Date == utcDate.Date)
                .OrderBy(e => e.StartTime)
                .ToListAsync();

            // 3. Получаем список невыполненных задач (Tasks)
            var tasks = await _context
                .Tasks.Where(t => t.UserId == userId && t.Status != TaskStatusType.Done)
                .ToListAsync();

            // 4. Базовый расчет весов (Приоритет + Дедлайн)
            var weightedTasks = tasks
                .Select(t => new { Task = t, BaseWeight = CalculateBaseWeight(t, utcDate) })
                .ToList();

            // 5. Очищаем старое сгенерированное расписание
            var oldEntries = _context.ScheduleEntries.Where(s =>
                s.UserId == userId && s.StartAt.Date == utcDate.Date
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
                        StartAt = ev.StartTime,
                        EndAt = ev.EndTime,
                        Status = ScheduleEntryStatus.Planned,
                        GenerationSource = GenerationSourceType.Manual,
                    }
                );
            }

            // 7. Алгоритм заполнения "окон" задачами
            DateTime currentPointer = dayStart;

            var overlappingEvent = schedule
                .Where(s => s.StartAt <= dayStart && s.EndAt > dayStart)
                .OrderByDescending(s => s.EndAt)
                .FirstOrDefault();

            if (overlappingEvent != null)
            {
                currentPointer = overlappingEvent.EndAt;
            }

            long? lastLifeAreaId = null;
            int tasksScheduledToday = 0;
            int continuousWorkMinutes = 0;

            while (currentPointer < dayEnd && weightedTasks.Any() && tasksScheduledToday < maxTasks)
            {
                var activeOrNextSlot = schedule
                    .Where(s => s.EndAt > currentPointer) // Нас интересует всё, что заканчивается позже текущего момента
                    .OrderBy(s => s.StartAt)
                    .FirstOrDefault();

                // 2. Если мы сейчас находимся ВНУТРИ мероприятия (курсор попал на его время)
                if (activeOrNextSlot != null && activeOrNextSlot.StartAt <= currentPointer)
                {
                    currentPointer = activeOrNextSlot.EndAt; // Прыгаем в конец мероприятия
                    continuousWorkMinutes = 0;
                    continue;
                }

                // 3. Если мероприятий впереди нет — свободны до конца дня. Если есть — до его начала.
                DateTime nextBusyTime = activeOrNextSlot?.StartAt ?? dayEnd;
                int gapMinutes = (int)(nextBusyTime - currentPointer).TotalMinutes;

                // 4. Если окно слишком маленькое (меньше 15 минут) до следующего события
                if (gapMinutes < 15)
                {
                    if (activeOrNextSlot != null)
                    {
                        currentPointer = activeOrNextSlot.EndAt; // Сразу перепрыгиваем это маленькое окно и само событие
                        continuousWorkMinutes = 0;
                        continue;
                    }
                }

                // Проверка на необходимость длительного перерыва
                if (continuousWorkMinutes >= breakInterval)
                {
                    if (gapMinutes >= breakDuration)
                    {
                        currentPointer = currentPointer.AddMinutes(breakDuration);
                        continuousWorkMinutes = 0; // Сбрасываем счетчик непрерывной работы
                        continue; // Идем на следующий круг, чтобы пересчитать gapMinutes
                    }
                    else
                    {
                        // В этом окне нет места для перерыва, прыгаем сразу за событие
                        currentPointer = nextBusyTime;
                        continuousWorkMinutes = 0; // Событие/сдвиг считается сменой деятельности (перерывом от задач)
                        continue;
                    }
                }

                if (gapMinutes >= 15) // Минимальный слот для планирования
                {
                    var currentTimeOfDay = currentPointer.TimeOfDay;
                    bool isPeakTime =
                        prefs != null
                        && prefs.PeakProductivityStart.HasValue
                        && prefs.PeakProductivityEnd.HasValue
                        && currentTimeOfDay >= prefs.PeakProductivityStart.Value
                        && currentTimeOfDay <= prefs.PeakProductivityEnd.Value;

                    // Динамический скоринг задач конкретно под текущее время (currentPointer)
                    var bestTaskMatch = weightedTasks
                        .Where(t => t.Task.EstimatedDuration <= gapMinutes)
                        .OrderByDescending(t =>
                        {
                            double dynamicScore = t.BaseWeight;

                            // Бонус 1: Чередование сфер жизни
                            if (t.Task.LifeAreaId != lastLifeAreaId)
                                dynamicScore += 10;

                            // Бонус 2: Учет биоритмов (Пиковая продуктивность)
                            if (isPeakTime && t.Task.EnergyRequired >= 4)
                            {
                                dynamicScore += 15; // Ресурсоемкие задачи в пиковое время
                            }
                            else if (!isPeakTime && t.Task.EnergyRequired <= 2)
                            {
                                dynamicScore += 5; // Легкие задачи (рутина) вне пика
                            }

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

                        // Обновляем счетчики и указатели
                        currentPointer = currentPointer.AddMinutes(task.EstimatedDuration + 5);
                        continuousWorkMinutes += task.EstimatedDuration;
                        lastLifeAreaId = task.LifeAreaId;
                        tasksScheduledToday++;

                        weightedTasks.Remove(bestTaskMatch);
                        continue;
                    }
                }

                // Если не нашли задачу для текущего окна или окно слишком маленькое, двигаемся к концу этого препятствия
                var nextEvent = schedule
                    .Where(s => s.StartAt >= currentPointer)
                    .OrderBy(s => s.StartAt)
                    .FirstOrDefault();
                if (nextEvent != null)
                {
                    currentPointer = nextEvent.EndAt;
                    continuousWorkMinutes = 0; // Событие обнуляет счетчик непрерывной фокусной работы
                }
                else
                {
                    break; // Нет ни событий, ни подходящих задач
                }
            }

            await _context.ScheduleEntries.AddRangeAsync(schedule);
            await _context.SaveChangesAsync();
        }

        private double CalculateBaseWeight(TaskItem task, DateTime targetDate)
        {
            double weight = task.Priority * PriorityWeight;

            if (task.Deadline.HasValue)
            {
                double daysUntilDeadline = (task.Deadline.Value - targetDate).TotalDays;
                if (daysUntilDeadline <= 0)
                    weight += 15; // Просрочено - наивысший приоритет
                else
                    weight += (1.0 / daysUntilDeadline) * UrgencyWeight * 10;
            }

            return weight;
        }
    }
}
