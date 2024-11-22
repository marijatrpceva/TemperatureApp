using ScottPlot;
using System.Globalization;
using System.IO.Ports;
using TemperatureApp;

namespace ConsoleApp1
{
    class Program
    {
        static async Task Main(string[] args)
        {

            string portName = "COM4";

            var mlModel = new MLModel();

            List<TemperaturePrediction> predictions = new List<TemperaturePrediction>();

            // Get the latest sensor data from the database
            SensorData? latestSensorData = await AccessToDb.GetLatestSensorDataFromSQLAsync();
            if (latestSensorData == null)
            {
                Console.WriteLine("No sensor data available to make predictions.");
                return;
            }

            DateTime currentTime = DateTime.Now;


            // Start reading sensor data and store it in the database in the background
            var cts = new CancellationTokenSource();
            Task sensorDataTask = ReadAndStoreSensorDataAsync(portName, cts.Token);

            Console.WriteLine("Press any key to stop the background sensor data reading...");
            Console.ReadKey();  // Wait for user input to stop

            cts.Cancel();  // Signal the background task to stop
            await sensorDataTask;  // Wait for the task to complete 

            // Generate predictions based on the latest sensor data
            for (int hoursAhead = 1; hoursAhead <= 5; hoursAhead++)
            {
                DateTime futureTime = currentTime.AddHours(hoursAhead);

                SensorData futureData = new SensorData
                {
                    Temperature = latestSensorData.Temperature,
                    Humidity = latestSensorData.Humidity,
                    ReadingTimestamp = futureTime
                };
                SensorDataWithTimeFeatures futureDataWithTimeFeatures = new SensorDataWithTimeFeatures
                {
                    Temperature = futureData.Temperature,
                    Humidity = futureData.Humidity,
                    Hour = futureData.ReadingTimestamp.Hour,
                    DayOfWeek = (float)futureData.ReadingTimestamp.DayOfWeek,
                    Month = futureData.ReadingTimestamp.Month
                };

                float predictedTemperature = await mlModel.PredictTemperatureAsync(futureDataWithTimeFeatures);
                predictions.Add(new TemperaturePrediction
                {
                    PredictedTemperature = predictedTemperature,
                    PredictionTimestamp = futureTime
                });
            }

            // Insert predictions into the database
            await AccessToDb.InsertPredictionsToSQLAsync(predictions);

            var (xValues, yValues) = AccessToDb.GetTemperaturePredictions();
            var plt = new Plot();

            plt.Add.Scatter(xValues, yValues);

            string[] labels = Array.ConvertAll(xValues, x => DateTime.FromOADate(x).ToString("HH:mm"));
            plt.Axes.Bottom.SetTicks(xValues, labels);

            plt.Title("Temperature prediction");
            plt.XLabel("Time");
            plt.YLabel("Temperature");

            plt.SavePng("prediction.png", 600, 400);

            System.Diagnostics.Process.Start("explorer", "prediction.png");

            Console.WriteLine("Application stopped.");
        }

        // Initialize and configure the serial port
        static SerialPort InitializeSerialPort(string portName)
        {
            return new SerialPort()
            {
                PortName = portName,
                BaudRate = 9600,
                DtrEnable = true,
                ReadTimeout = 5000,
                WriteTimeout = 500
            };
        }

        // Parse the sensor data from the string format
        static (float temperature, float humidity) ParseSensorData(string data)
        {
            string[] sensorValues = data.Split(',');

            // Replace '.' with ',' for cultures that use commas as decimal points
            string temperatureString = sensorValues[0].Replace('.', ',');
            string humidityString = sensorValues[1].Replace('.', ',');

            // Parse temperature and humidity using the current culture
            float temperature = float.Parse(temperatureString, CultureInfo.CurrentCulture);
            float humidity = float.Parse(humidityString, CultureInfo.CurrentCulture);

            return (temperature, humidity);
        }

        // Method to read sensor data and store it in the database
        static async Task ReadAndStoreSensorDataAsync(string portName, CancellationToken cancellationToken)
        {
            using (SerialPort port = InitializeSerialPort(portName))
            {
                try
                {
                    port.Open();
                    Console.WriteLine("Port opened successfully.");

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            string data = port.ReadLine();
                            Console.WriteLine($"Data received: {data}");
                            (float temperature, float humidity) = ParseSensorData(data);

                            // Validate the sensor data range before storing
                            if (temperature < 0 || temperature > 50 || humidity < 20 || humidity > 90)
                            {
                                Console.WriteLine("Invalid data received, skipping...");
                                continue;
                            }

                            // Insert the sensor data into the database
                            await AccessToDb.InsertBatchSensorData(temperature, humidity);
                            Console.WriteLine($"Data stored: Temp={temperature}, Humidity={humidity}");
                        }
                        catch (TimeoutException)
                        {
                            Console.WriteLine("Error: Timeout while reading from port.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"An error occurred: {ex.Message}");
                        }

                        await Task.Delay(5000);  // Wait for the next reading and support cancellation
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cannot open port: {ex.Message}");
                }
                finally
                {
                    if (port.IsOpen)
                    {
                        port.Close();
                        Console.WriteLine("Port closed.");
                    }
                }
            }
        }
    }
}
