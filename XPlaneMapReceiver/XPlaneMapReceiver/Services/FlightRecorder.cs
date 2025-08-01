using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace XPlaneMapReceiver.Services
{
    public class FlightRecord
    {
        public DateTime Timestamp { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public string TailNumber { get; set; }
        public bool OnGround { get; set; }
        public double Heading { get; set; } // Yeni eklenen heading özelliği
    }

    public class FlightRecorder
    {
        private List<FlightRecord> records = new List<FlightRecord>();
        private bool isRecording = false;
        private bool isPaused = false;
        private string tailNumber = "";

        public string DepartureICAO { get; private set; } = "";
        public string ArrivalICAO { get; private set; } = "";

        public bool IsRecording => isRecording;
        public bool IsPaused => isPaused;

        public void Start(string tailNum = "", string departureIcao = "", string arrivalIcao = "")
        {
            records.Clear();
            isRecording = true;
            isPaused = false;
            tailNumber = tailNum;
            DepartureICAO = departureIcao;
            ArrivalICAO = arrivalIcao;
        }

        public void Stop()
        {
            isRecording = false;
            isPaused = false;
        }

        public void Pause()
        {
            if (isRecording) isPaused = true;
        }

        public void Resume()
        {
            if (isRecording) isPaused = false;
        }

        public void AddRecord(double lat, double lon, double altitude, string tailNum, bool onGround, double heading = 0)
        {
            if (!isRecording || isPaused) return;
            records.Add(new FlightRecord
            {
                Timestamp = DateTime.UtcNow,
                Latitude = lat,
                Longitude = lon,
                Altitude = altitude,
                TailNumber = tailNum,
                OnGround = onGround,
                Heading = heading
            });
        }

        public void SaveToCsv(string filePath)
        {
            using (var sw = new StreamWriter(filePath))
            {
                // Meta satırı
                sw.WriteLine($"#DepartureICAO={DepartureICAO},ArrivalICAO={ArrivalICAO}");
                sw.WriteLine("Timestamp,Latitude,Longitude,Altitude,TailNumber,OnGround,Heading");
                foreach (var rec in records)
                {
                    sw.WriteLine($"{rec.Timestamp:O},{rec.Latitude.ToString(CultureInfo.InvariantCulture)},{rec.Longitude.ToString(CultureInfo.InvariantCulture)},{rec.Altitude.ToString(CultureInfo.InvariantCulture)},{rec.TailNumber},{rec.OnGround},{rec.Heading.ToString(CultureInfo.InvariantCulture)}");
                }
            }
        }

        public void SaveToJson(string filePath)
        {
            var obj = new
            {
                DepartureICAO = this.DepartureICAO,
                ArrivalICAO = this.ArrivalICAO,
                Records = records
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(filePath, JsonSerializer.Serialize(obj, options));
        }

        public List<FlightRecord> GetRecords() => new List<FlightRecord>(records);
    }
}
