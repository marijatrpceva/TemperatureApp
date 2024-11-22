using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemperatureApp
{
    public class MLModel
    {
        private static MLContext mlContext = new MLContext();
        private ITransformer model;

        public MLModel()
        {
            // Load historical data from SQL
            List<SensorData> sensorDataList = AccessToDb.GetSensorData();

            // Preprocess the data to extract time features
            List<SensorDataWithTimeFeatures> processedData = PreprocessData(sensorDataList);

            // Load the preprocessed data into an IDataView for ML.NET
            IDataView dataView = mlContext.Data.LoadFromEnumerable(processedData);

            // Define the ML pipeline
            var pipeline = mlContext.Transforms
                .CopyColumns(outputColumnName: "Label", inputColumnName: "Temperature")
                .Append(mlContext.Transforms.Concatenate("Features","Humidity","Hour","DayOfWeek","Month"))
                .Append(mlContext.Regression.Trainers.Sdca());  // SDCA regression trainer

            // Train the model
            model = pipeline.Fit(dataView);
        }

        // Predict the future temperature
        // Asynchronous prediction method
        public async Task<float> PredictTemperatureAsync(SensorDataWithTimeFeatures input)
        {
            var predictionEngine = mlContext.Model
                .CreatePredictionEngine<SensorDataWithTimeFeatures, TemperaturePrediction>(model);
            var prediction = predictionEngine.Predict(input);
            return await Task.FromResult(prediction.PredictedTemperature);
        }
        public List<SensorDataWithTimeFeatures> PreprocessData(List<SensorData> sensorDataList)
        {
            var processedData = sensorDataList.Select(data => new SensorDataWithTimeFeatures
            {
                Temperature = data.Temperature,
                Humidity = data.Humidity,
                Hour = data.ReadingTimestamp.Hour,              
                DayOfWeek = (float)data.ReadingTimestamp.DayOfWeek,
                Month = data.ReadingTimestamp.Month            
            }).ToList();

            return processedData;
        }
        
    }

}
