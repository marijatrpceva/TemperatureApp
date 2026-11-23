# TemperatureApp

**A thesis project combining sensor data collection, SQL Server storage, and temperature prediction with ML.NET.**

TemperatureApp is a C# console application developed as part of my thesis. It reads temperature and humidity measurements from a serial-connected device, stores readings in a SQL Server database, and uses historical measurements to train a regression model. The application then estimates temperature for five hourly timestamps and exports a chart of the predictions.

The project brings together hardware communication, database access, feature engineering, machine learning, and data visualization in a single application.

## Features

- **Sensor integration:** Reads newline-terminated temperature and humidity pairs over a serial connection.
- **Input validation:** Accepts temperature values from 0 to 50 and humidity values from 20 to 90, inclusive.
- **Persistent storage:** Saves sensor readings and predictions to SQL Server using parameterized SQL commands.
- **Feature engineering:** Extracts hour, day of week, and month from measurement timestamps.
- **Machine learning:** Trains an ML.NET SDCA regression model using historical data.
- **Temperature estimates:** Generates predictions for five hourly timestamps using time features and a fixed humidity value from the latest reading retrieved at startup.
- **Visualization:** Creates a 600 × 400 PNG chart with ScottPlot and opens it through Windows Explorer.

## Technology stack

| Component | Technology |
| --- | --- |
| Language and runtime | C#, .NET 8 |
| Machine learning | ML.NET, SDCA regression |
| Database | Microsoft SQL Server |
| Database access | ADO.NET / `System.Data.SqlClient` |
| Device communication | `System.IO.Ports` |
| Charts | ScottPlot 5 |
| Configuration | `Microsoft.Extensions.Configuration.Json` |

Dependency versions are defined in `TemperatureApp/TemperatureApp.csproj`. The project currently references preview or release-candidate versions of several packages.

## How it works

```text
Historical readings in SQL Server
            |
            v
Extract humidity and time features --> Train SDCA regression model
                                                   |
Serial device --> Parse and validate --> SQL Server |
                                                   v
Startup reading + five future timestamps --> Temperature estimates
                                                   |
                                                   v
                                SQL Server --> prediction.png
```

The current execution order is:

1. Load historical sensor readings from SQL Server and train the model.
2. Retrieve the latest stored reading and capture the current local time.
3. Start reading from the serial device and storing valid measurements.
4. Wait for a keypress to stop collection, then wait for the collection task to finish.
5. Generate five predictions for one through five hours after the time captured in step 2.
6. Store the predictions, retrieve the five most recently inserted predictions, and plot them.

The model and prediction inputs are prepared before the collection session. Measurements collected during that session become available for training on the next application run; the model is not retrained during the session. If collection lasts a long time, some prediction timestamps may already be in the past when the chart is generated.

## Machine learning approach

The application uses supervised regression to learn a relationship between historical temperature, humidity, and calendar features.

| Role | Field | Meaning |
| --- | --- | --- |
| Training label | `Temperature` | Observed temperature to predict |
| Input feature | `Humidity` | Humidity reading |
| Input feature | `Hour` | Hour of the day, 0–23 |
| Input feature | `DayOfWeek` | Numeric day of week, Sunday = 0 |
| Input feature | `Month` | Month of the year, 1–12 |

The ML.NET pipeline copies `Temperature` into the label column, combines the four input features into a feature vector, and trains an SDCA (Stochastic Dual Coordinate Ascent) regression model. The predicted value is read from ML.NET's `Score` output.

For prediction, the application keeps the startup reading's humidity constant while changing the timestamp features. Although the prediction input object also contains a temperature value, that value is not included in the feature vector.

This is a regression-based temperature estimation prototype. It does not implement a dedicated time-series forecasting model, lagged temperature features, or future humidity prediction. The repository contains no evaluation metrics, so no prediction accuracy is claimed.

## Project structure

```text
TemperatureApp.sln
TemperatureApp/
├── Program.cs                     # Serial collection, prediction workflow, and chart export
├── AccessToDb.cs                  # SQL Server reads and writes
├── MLModel.cs                     # Feature preparation, training, and inference
├── SensorData.cs                  # Temperature, humidity, and measurement timestamp
├── SensorDataWithTimeFeatures.cs  # Model input fields
├── TemperaturePrediction.cs      # Predicted value and associated timestamp
├── TemperatureModel.mbconfig     # Model Builder regression configuration
└── TemperatureApp.csproj          # Runtime target and dependencies
```

Training is implemented directly in `MLModel.cs`; the application does not load a pretrained model from the `.mbconfig` file.

## Prerequisites

- Windows for the current COM-port configuration and Explorer-based chart opening.
- .NET 8 SDK.
- An accessible SQL Server instance and permission to read and insert data.
- A serial-connected device that sends temperature and humidity pairs at 9600 baud.
- Historical sensor readings already stored in the database.

Device firmware, the original database schema, historical data, and a trained model are not included in the repository. The hardware model is not specified in the source.

## Setup and running

### 1. Get the source

```powershell
git clone https://github.com/marijatrpceva/TemperatureApp.git
cd TemperatureApp
dotnet restore TemperatureApp.sln
```

### 2. Prepare SQL Server

Create a database such as `TemperatureDb`. The application expects tables named `SensorData` and `TemperaturePredictions`.

The following is a **suggested minimal schema inferred from the SQL queries**, not the original thesis database script. Run it against your chosen database if you are setting up a new instance:

```sql
CREATE TABLE dbo.SensorData
(
    SensorDataID INT IDENTITY(1,1) PRIMARY KEY,
    SensorID INT NOT NULL,
    Temperature DECIMAL(10,4) NOT NULL,
    Humidity DECIMAL(10,4) NOT NULL,
    ReadingTimestamp DATETIME NOT NULL DEFAULT (GETDATE())
);

CREATE TABLE dbo.TemperaturePredictions
(
    PredictionID INT IDENTITY(1,1) PRIMARY KEY,
    SensorID INT NOT NULL,
    PredictedTemperature DECIMAL(10,4) NOT NULL,
    PredictionTimestamp DATETIME NOT NULL
);
```

The decimal types match the application's decimal reads, and the default reading timestamp is needed because inserts supply only the sensor ID, temperature, and humidity. The current implementation writes `SensorID = 1` and reads historical data without filtering by sensor.

Import historical measurements into `SensorData` before launching the application. Training runs before serial collection, so an empty database cannot be initialized through the current application workflow. A single row is not a meaningful training dataset; use varied historical measurements for useful experimentation.

### 3. Configure the connection

Create `appsettings.json` inside the inner `TemperatureApp` project directory:

```json
{
  "ConnectionStrings": {
    "ConnectionString": "Server=localhost;Database=TemperatureDb;Integrated Security=True;"
  }
}
```

Adjust the server, database, and authentication settings for your SQL Server instance. `appsettings.json` is already excluded by `.gitignore`.

The application looks for this file in its current working directory. The run commands below use the project directory so the file can be found.

### 4. Configure the device

In `TemperatureApp/Program.cs`, change `portName` from `COM4` to the port assigned to your device.

The configured connection uses 9600 baud, DTR enabled, a 5-second read timeout, and a 500-millisecond write timeout. Send one comma-separated pair per line, in this order:

```text
23.5,55.2
```

The parser currently replaces decimal points with commas and then uses `CultureInfo.CurrentCulture`. Its behavior therefore depends on the machine's locale. Before using a dot-decimal device format on another machine, check the parsing behavior; a portability improvement would be to parse both values with `CultureInfo.InvariantCulture` without replacing decimal points.

### 5. Build and run

From the repository root:

```powershell
dotnet build TemperatureApp.sln
cd TemperatureApp
dotnet run
```

Let the application collect readings, then press any key to stop collection and generate the predictions. Shutdown may take several seconds while a serial read or the loop's delay finishes.

The application stores the predictions in SQL Server, saves `prediction.png` in the current working directory, and attempts to open it through Windows Explorer. Console messages report received readings, successful inserts, and serial or parsing errors.

## Scope and possible improvements

The project demonstrates integration of sensor communication, relational storage, machine learning, and chart generation. Its current scope is a thesis prototype, with several opportunities for further development:

- Evaluate predictions on a chronological holdout dataset and report MAE, RMSE, and R² against a simple baseline.
- Refresh the latest measurement and reference time after collection, and retrain when appropriate.
- Add lagged readings and cyclic encodings for hour, weekday, and month.
- Make serial settings and sensor IDs configurable, and handle an empty training dataset before training.
- Use culture-independent parsing and validate the number of incoming fields.
- Move blocking serial reads off the main execution path and make delays cancellation-aware.
- Save trained models and add automated tests for parsing, feature extraction, and database behavior.

The repository does not include automated tests or model evaluation results. Setup instructions describe the source's requirements; a complete run requires the external database and serial device.

## Author

[Marija Trpceva](https://github.com/marijatrpceva) — developed as part of a thesis project.
