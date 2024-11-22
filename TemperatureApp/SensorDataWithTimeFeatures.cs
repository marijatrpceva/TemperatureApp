namespace TemperatureApp
{
    public class SensorDataWithTimeFeatures
    {
        public float Temperature { get; set; }
        public float Humidity { get; set; }
        public float Hour { get; set; }
        public float DayOfWeek { get; set; }
        public float Month { get; set; }
    }
}
