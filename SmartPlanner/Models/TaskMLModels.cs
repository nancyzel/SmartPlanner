using Microsoft.ML.Data;

namespace SmartPlanner.Models
{
    // Класс, описывающий параметры задачи, которые подаются на вход ML-модели
    public class TaskPredictorInput
    {
        [LoadColumn(0)]
        public float Priority { get; set; }

        [LoadColumn(1)]
        public float EnergyRequired { get; set; }

        [LoadColumn(2)]
        public float LifeAreaId { get; set; }

        [LoadColumn(3)]
        public float EstimatedDuration { get; set; }

        // Целевой признак (Y), который мы пытаемся предсказать
        [LoadColumn(4)]
        public float ActualDuration { get; set; }
    }

    // Класс, который возвращает модель после расчета
    public class TaskPredictorOutput
    {
        // ML.NET по умолчанию называет результат "Score" для задач регрессии
        [ColumnName("Score")]
        public float PredictedDuration { get; set; }
    }
}
