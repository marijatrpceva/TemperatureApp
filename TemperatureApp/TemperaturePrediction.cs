using Microsoft.ML.Data;

public class TemperaturePrediction
{
    [ColumnName("Score")]
    public float PredictedTemperature { get; set; }
    public DateTime PredictionTimestamp { get; set; }
}
