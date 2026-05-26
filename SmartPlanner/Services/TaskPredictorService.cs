using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using SmartPlanner.Models;

namespace SmartPlanner.Services
{
    public class TaskPredictorService
    {
        private readonly ApplicationDbContext _context;
        private readonly string _modelPath;
        private readonly MLContext _mlContext;

        public TaskPredictorService(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _mlContext = new MLContext(seed: 1);
            // Путь для сохранения обученной модели на сервере
            _modelPath = Path.Combine(env.ContentRootPath, "MLModels", "task_duration_model.zip");
        }

        // 1. Метод предсказания длительности задачи
        public int PredictDuration(TaskItem task)
        {
            if (!File.Exists(_modelPath))
            {
                // Если модель еще ни разу не обучалась, возвращаем экспертную оценку пользователя
                return task.EstimatedDuration;
            }

            // Загружаем обученную модель
            ITransformer trainedModel;
            using (
                var stream = new FileStream(
                    _modelPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                )
            )
            {
                trainedModel = _mlContext.Model.Load(stream, out _);
            }

            // Создаем движок предсказания
            var predictionEngine = _mlContext.Model.CreatePredictionEngine<
                TaskPredictorInput,
                TaskPredictorOutput
            >(trainedModel);

            // Формируем входные данные
            var input = new TaskPredictorInput
            {
                Priority = task.Priority,
                EnergyRequired = task.EnergyRequired,
                LifeAreaId = task.LifeAreaId ?? 0,
                EstimatedDuration = task.EstimatedDuration,
            };

            // Предсказываем
            var prediction = predictionEngine.Predict(input);

            int predicted = Math.Max(5, (int)Math.Round(prediction.PredictedDuration));

            if (Math.Abs(predicted - task.EstimatedDuration) < 5)
            {
                return task.EstimatedDuration;
            }

            return predicted;
        }

        // 2. Метод обучения модели на основе истории выполненных задач
        public async Task TrainModelAsync()
        {
            // Берем только выполненные задачи, где заполнено реальное время выполнения
            var completedTasks = await _context
                .Tasks.Where(t =>
                    t.Status == Models.Enums.TaskStatusType.Done && t.ActualDuration.HasValue
                )
                .ToListAsync();

            // Модели нужно хотя бы 5-10 примеров для минимального обучения
            if (completedTasks.Count < 5)
                return;

            // Преобразуем данные из БД в формат для ML
            var trainingData = completedTasks
                .Select(t => new TaskPredictorInput
                {
                    Priority = t.Priority,
                    EnergyRequired = t.EnergyRequired,
                    LifeAreaId = t.LifeAreaId ?? 0,
                    EstimatedDuration = t.EstimatedDuration,
                    ActualDuration = (float)t.ActualDuration.Value,
                })
                .ToList();

            IDataView dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

            // Построение конвейера (Pipeline) обучения
            // Шаг 1: Объединяем признаки в один вектор "Features"
            var pipeline = _mlContext
                .Transforms.Concatenate(
                    "Features",
                    nameof(TaskPredictorInput.Priority),
                    nameof(TaskPredictorInput.EnergyRequired),
                    nameof(TaskPredictorInput.LifeAreaId),
                    nameof(TaskPredictorInput.EstimatedDuration)
                )
                // Шаг 2: Выбираем алгоритм регрессии (FastTree — один из лучших для таких задач)
                .Append(
                    _mlContext.Regression.Trainers.FastTree(
                        labelColumnName: nameof(TaskPredictorInput.ActualDuration)
                    )
                );

            // Обучаем модель
            var model = pipeline.Fit(dataView);

            // Создаем папку, если её нет, и сохраняем модель в zip-файл
            var directory = Path.GetDirectoryName(_modelPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory!);

            using (
                var stream = new FileStream(
                    _modelPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None
                )
            )
            {
                _mlContext.Model.Save(model, dataView.Schema, stream);
            }
        }
    }
}
