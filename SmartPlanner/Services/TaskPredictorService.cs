using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Trainers;
using SmartPlanner.Models;

namespace SmartPlanner.Services
{
    public class TaskPredictorService
    {
        private readonly ApplicationDbContext _context;
        private readonly string _modelPath;
        private readonly MLContext _mlContext;

        // Порог переключения алгоритма: до 30 задач используем простую линейную регрессию, после — FastTree
        private const int TasksThresholdForAdvancedModel = 30;

        public TaskPredictorService(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _mlContext = new MLContext(seed: 1);
            _modelPath = Path.Combine(env.ContentRootPath, "MLModels", "task_duration_model.zip");
        }

        // 1. Метод предсказания длительности задачи
        public int PredictDuration(TaskItem task)
        {
            if (!File.Exists(_modelPath))
            {
                return task.EstimatedDuration;
            }

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

            var predictionEngine = _mlContext.Model.CreatePredictionEngine<
                TaskPredictorInput,
                TaskPredictorOutput
            >(trainedModel);

            var input = new TaskPredictorInput
            {
                Priority = task.Priority,
                EnergyRequired = task.EnergyRequired,
                LifeAreaId = task.LifeAreaId ?? 0,
                EstimatedDuration = task.EstimatedDuration,
            };

            var prediction = predictionEngine.Predict(input);
            int predicted = Math.Max(5, (int)Math.Round(prediction.PredictedDuration));

            // Защита от нереалистичных выбросов ИИ (особенно актуально на малых выборках)
            if (predicted < 5 || predicted > (task.EstimatedDuration * 3))
            {
                return task.EstimatedDuration;
            }

            if (Math.Abs(predicted - task.EstimatedDuration) < 5)
            {
                return task.EstimatedDuration;
            }

            return predicted;
        }

        // 2. Метод адаптивного обучения модели
        public async Task TrainModelAsync()
        {
            var completedTasks = await _context
                .Tasks.Include(t => t.MLTrainingData)
                .Where(t =>
                    t.Status == Models.Enums.TaskStatusType.Done && t.ActualDuration.HasValue
                )
                .ToListAsync();

            // Абсолютный минимум для построения базовой математической регрессии
            if (completedTasks.Count < 5)
                return;

            var trainingData = completedTasks
                .Where(t => t.MLTrainingData.Any())
                .Select(t =>
                {
                    var mlLog = t.MLTrainingData.OrderByDescending(m => m.CreatedAt).First();

                    return new TaskPredictorInput
                    {
                        Priority = t.Priority,
                        EnergyRequired = t.EnergyRequired,
                        LifeAreaId = t.LifeAreaId ?? 0,
                        EstimatedDuration = mlLog.UserOriginalDuration,
                        ActualDuration = (float)t.ActualDuration.Value,
                    };
                })
                .ToList();

            IDataView dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

            // Базовая подготовка признаков (склеивание и обязательная нормализация масштабов)
            var pipeline = _mlContext
                .Transforms.Concatenate(
                    "Features",
                    nameof(TaskPredictorInput.Priority),
                    nameof(TaskPredictorInput.EnergyRequired),
                    nameof(TaskPredictorInput.LifeAreaId),
                    nameof(TaskPredictorInput.EstimatedDuration)
                )
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"));

            IEstimator<ITransformer> trainingPipeline;

            // ДИНАМИЧЕСКИЙ ВЫБОР АЛГОРИТМА
            if (trainingData.Count < TasksThresholdForAdvancedModel)
            {
                // ЭТАП 1: Мало данных. Идеально подходит Ordinary Least Squares (OLS).
                // Он строит жесткую прямую линию тренда, не переобучается на шумах и не скатывается к константе.
                trainingPipeline = pipeline.Append(
                    _mlContext.Regression.Trainers.Ols(
                        labelColumnName: nameof(TaskPredictorInput.ActualDuration)
                    )
                );
            }
            else
            {
                // ЭТАП 2: Данных достаточно. Подключаем мощный нелинейный алгоритм FastTree.
                // На больших выборках ансамбли решающих деревьев отлично находят скрытые паттерны поведения пользователя.
                trainingPipeline = pipeline.Append(
                    _mlContext.Regression.Trainers.FastTree(
                        labelColumnName: nameof(TaskPredictorInput.ActualDuration)
                    )
                );
            }

            // Обучаем выбранную конфигурацию конвейера
            var model = trainingPipeline.Fit(dataView);

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
