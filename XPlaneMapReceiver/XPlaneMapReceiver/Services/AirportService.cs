using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using XPlaneMapReceiver.Models;

namespace XPlaneMapReceiver.Services
{
    public static class AirportService
    {
        public static List<Airport> LoadAirports(string csvPath)
        {
            var airports = new List<Airport>();
            using (var reader = new StreamReader(csvPath))
            {
                string header = reader.ReadLine(); // başlık satırını atla
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var parts = line.Split(',');
                    if (parts.Length < 6) continue;
                    airports.Add(new Airport
                    {
                        ICAO = parts[0],
                        Name = parts[1],
                        City = parts[2],
                        Country = parts[3],
                        Lat = parts[4],
                        Lon = parts[5]
                    });
                }
            }
            return airports;
        }

        public static List<Airport> GetVisibleAirports(List<Airport> airports, double currentZoom)
        {
            if (currentZoom <= 1.0)
            {
                // Tam dünya görünümünde sadece büyük havalimanları
                return airports.Where(ap => IsMajorAirport(ap)).Take(500).ToList();
            }
            else if (currentZoom <= 3.0)
            {
                // Orta zoom'da daha fazla havalimanı
                return airports.Where(ap => IsMediumAirport(ap)).Take(2000).ToList();
            }
            else
            {
                // Yüksek zoom'da tüm havalimanları
                return airports.Take(5000).ToList();
            }
        }

        private static bool IsMajorAirport(Airport airport)
        {
            // Büyük şehirlerdeki havalimanları
            string[] majorCities = { "LONDON", "PARIS", "NEW YORK", "TOKYO", "BEIJING", "MOSCOW", "ISTANBUL", "DUBAI", "SINGAPORE", "SYDNEY" };
            return majorCities.Any(city => airport.City?.ToUpper().Contains(city) == true ||
                                          airport.Name?.ToUpper().Contains(city) == true);
        }

        private static bool IsMediumAirport(Airport airport)
        {
            // Başkentler ve büyük şehirler
            string[] mediumCities = { "ANKARA", "IZMIR", "ANTALYA", "BERLIN", "MADRID", "ROME", "AMSTERDAM", "VIENNA", "PRAGUE", "BUDAPEST" };
            return IsMajorAirport(airport) || mediumCities.Any(city => airport.City?.ToUpper().Contains(city) == true);
        }

        public static int GetAirportDotSize(double currentZoom)
        {
            if (currentZoom <= 1.0) return 3;
            if (currentZoom <= 2.0) return 4;
            if (currentZoom <= 5.0) return 6;
            return 8;
        }
    }
}