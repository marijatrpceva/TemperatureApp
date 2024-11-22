using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;

namespace TemperatureApp
{
    public static class AccessToDb
    {
        private static readonly string _connectionString;

        // Static constructor to initialize the connection string
        static AccessToDb()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            _connectionString = configuration.GetConnectionString("ConnectionString");
        }
        public static async Task InsertPredictionsToSQLAsync(List<TemperaturePrediction> predictions)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                foreach (var prediction in predictions)
                {
                    string query = "INSERT INTO TemperaturePredictions " +
                        "(SensorID, PredictedTemperature, PredictionTimestamp) " +
                        "VALUES (@SensorID, @PredictedTemperature, @PredictionTimestamp)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@SensorID", 1);
                        command.Parameters.AddWithValue("@PredictedTemperature",
                            prediction.PredictedTemperature);
                        command.Parameters.AddWithValue("@PredictionTimestamp", 
                            prediction.PredictionTimestamp);

                        await command.ExecuteNonQueryAsync();  // Insert the data asynchronously
                    }
                }
            }
        }

        // Insert the batch of sensor data into the database asynchronously
        public static async Task InsertBatchSensorData(float temperature, float humidity)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                    string query = "INSERT INTO SensorData (SensorID, Temperature, Humidity) " +
                        "VALUES (@SensorID, @Temperature, @Humidity)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@SensorID", 1);
                        command.Parameters.AddWithValue("@Temperature", temperature);
                        command.Parameters.AddWithValue("@Humidity", humidity);

                        await command.ExecuteNonQueryAsync();  // Insert the data asynchronously
                    }
            }
        }

        // Asynchronously get the latest sensor data from the SQL database
        public static async Task<SensorData?> GetLatestSensorDataFromSQLAsync()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = "SELECT TOP 1 * FROM SensorData ORDER BY ReadingTimestamp DESC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new SensorData
                            {
                                Temperature = Convert.ToSingle(reader["Temperature"]),
                                Humidity = Convert.ToSingle(reader["Humidity"]),
                                ReadingTimestamp = (DateTime)reader["ReadingTimestamp"]
                            };
                        }
                    }
                }
            }
            return null;
        }

        public static (double[] xValues, double[] yValues) GetTemperaturePredictions()
        {
            List<double> xList = new List<double>();
            List<double> yList = new List<double>();

            string query = @"
                SELECT TOP 5 PredictionTimestamp, PredictedTemperature 
                FROM TemperaturePredictions 
                ORDER BY PredictionID DESC";

            // Connect to the database and retrieve data
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(query, conn);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime timestamp = reader.GetDateTime(0); // First column is PredictionTimestamp (datetime)
                        decimal temperatureDecimal = reader.GetDecimal(1); // Second column is PredictedTemperature (decimal)
                        double temperature = (double)temperatureDecimal; // Convert decimal to double

                        xList.Add(timestamp.ToOADate()); // Convert timestamp to OADate for ScottPlot
                        yList.Add(temperature);
                    }
                }
            }

            xList.Reverse();
            yList.Reverse();

            return (xList.ToArray(), yList.ToArray());
        }

        public static List<SensorData> GetSensorData()
        {
            List<SensorData> sensorDataList = new List<SensorData>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT Temperature, Humidity, ReadingTimestamp FROM SensorData";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        sensorDataList.Add(new SensorData
                        {
                            Temperature = (float)reader.GetDecimal(0),
                            Humidity = (float)reader.GetDecimal(1),
                            ReadingTimestamp = reader.GetDateTime(2)
                        });
                    }
                }
            }
            return sensorDataList;
        }
    }


}
