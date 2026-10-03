using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XPlaneMapReceiver.Models;
using XPlaneMapReceiver.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.IO;
using System.Text.Json;
using System.Speech.Synthesis;

namespace XPlaneMapReceiver
{
    public partial class Form1 : Form
    {
        private List<Airport> airports;
        // Havalimanı verisi uygulamanın yanındaki Data klasöründen okunur (tools/build_airports.py ile üretilir)
        private static readonly string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        private string csvPath = Path.Combine(dataDir, "airports.csv");
        private const string MapHost = "xplanemap.local";

        // XPlaneMapPlugin'in UDP mesajındaki alan sırası (XPlaneMapPlugin.cpp, udpMsg)
        private const int PluginFieldCount = 32;
        private const int HeadingIndex = 23;
        private const int AirspeedIndex = 29;
        private const int AglIndex = 31;
        private DateTime lastDataLogTime = DateTime.MinValue;

        // Services
        private UdpService udpService;

        // UI State
        private Timer redrawTimer;

        private WebView2 webView;

        private FlightRecorder flightRecorder = new FlightRecorder();
        private List<FlightRecord> replayRecords = null;
        private int replayIndex = 0;
        private Timer replayTimer = null;
        private string lastTailNum = "";
        // TrackBar ve Label için alanlar
        private double replaySpeed = 1.0; // 1x
        // CSV meta için
        private string replayDepartureICAO = null;
        private string replayArrivalICAO = null;

        private SpeechSynthesizer speechSynth = new SpeechSynthesizer();
        private bool lastOnGround = true;
        private bool isFirstUdp = true;

        // Checklist için timer ve kontrol
        private Timer checklistTimer = null;
        private bool checklistAnnounced = false;

        public Form1()
        {
            InitializeComponent();

            // WebView2 başlat
            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            this.Controls.Add(webView);

            // JSON dosyasını oku (yoksa harita havalimanları olmadan açılır)
            string jsonPath = Path.Combine(dataDir, "airports.json");
            string json = File.Exists(jsonPath) ? File.ReadAllText(jsonPath) : "[]";

            // WebView2 yüklendikten sonra haritayı aç ve JS'ye markerları gönder
            webView.CoreWebView2InitializationCompleted += (s, e) =>
            {
                // map.html file:// yerine sanal bir https adresinden açılır; OpenStreetMap
                // döşeme isteklerinde geçerli bir Referer bekliyor, file:// ile 403 dönüyor
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    MapHost, AppDomain.CurrentDomain.BaseDirectory, CoreWebView2HostResourceAccessKind.Allow);
                webView.CoreWebView2.Navigate($"https://{MapHost}/map.html");

                // Harita dışındaki bağlantılar (ör. attribution) varsayılan tarayıcıda açılır, harita kaybolmaz
                webView.CoreWebView2.NavigationStarting += (s3, e3) =>
                {
                    if (!e3.Uri.StartsWith($"https://{MapHost}/"))
                    {
                        e3.Cancel = true;
                        System.Diagnostics.Process.Start(e3.Uri);
                    }
                };
                webView.CoreWebView2.NewWindowRequested += (s3, e3) =>
                {
                    e3.Handled = true;
                    System.Diagnostics.Process.Start(e3.Uri);
                };

                webView.CoreWebView2.WebMessageReceived += (s2, e2) =>
                {
                    if (e2.TryGetWebMessageAsString() == "ready")
                    {
                        string script = $"window.addAirports({json});";
                        webView.ExecuteScriptAsync(script);
                    }
                };
            };
            _ = webView.EnsureCoreWebView2Async();

            // Initialize services
            udpService = new UdpService();

            // Setup UI events
            SetupButtons();
            SetupTimer();

            // Load airports
            LoadAirports();
            SetupComboBoxes();

            // Kayıt ve replay butonlarını bağla
            btnStartRecording.Click += btnStartRecording_Click;
            btnStopRecording.Click += btnStopRecording_Click;
            btnOpenRecording.Click += btnOpenRecording_Click;
            btnReplay.Click += btnReplay_Click;
            btnPauseReplay.Click += btnPauseReplay_Click;
            btnResumeReplay.Click += btnResumeReplay_Click;
            trackBarReplaySpeed.ValueChanged += trackBarReplaySpeed_ValueChanged;
            btnForward.Click += btnForward_Click;
            btnRewind.Click += btnRewind_Click;

            // Start UDP listener
            StartUdpListener();

            speechSynth.SelectVoice("Microsoft David Desktop");
            speechSynth.Rate = 0;
            speechSynth.Volume = 100;

            // Checklist timer'ı oluştur
            checklistTimer = new Timer();
            checklistTimer.Interval = 5000; // 5 saniye
            checklistTimer.Tick += ChecklistTimer_Tick;
        }

        private void SetupUIEvents()
        {
            // Artık PictureBox mouse eventleri yok
        }

        private void SetupButtons()
        {
            // Debug için test butonu ekle
            Button testButton = new Button();
            testButton.Text = "Test Havalimanları";
            testButton.Location = new Point(10, 10);
            testButton.Size = new Size(80, 25);
            testButton.Click += TestButton_Click;
            this.Controls.Add(testButton);

            // Zoom butonları kaldırıldı
            // Start Flight butonu ekle
            Button startFlightButton = new Button();
            startFlightButton.Name = "startFlightButton";
            startFlightButton.Text = "Start Flight";
            startFlightButton.Location = new Point(10, 100);
            startFlightButton.Size = new Size(80, 25);
            startFlightButton.Click += StartFlightButton_Click;
            this.Controls.Add(startFlightButton);
        }

        private void SetupTimer()
        {
            // Redraw timer'ı
            redrawTimer = new Timer();
            redrawTimer.Interval = 100; // 100ms
            redrawTimer.Tick += RedrawTimer_Tick;
        }

        private void LoadAirports()
        {
            try
            {
                airports = AirportService.LoadAirports(csvPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Havalimanı verisi yüklenemedi:\n" + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                airports = new List<Airport>();
            }
        }

        private void SetupComboBoxes()
        {
            comboBoxDeparture.Items.Add("Free Flight");
            comboBoxArrival.Items.Add("Free Flight");
            foreach (var ap in airports)
            {
                string display = $"{ap.ICAO} - {ap.Name}";
                if (!string.IsNullOrWhiteSpace(ap.City) || !string.IsNullOrWhiteSpace(ap.Country))
                    display += $" ({ap.City}, {ap.Country})";
                comboBoxDeparture.Items.Add(display);
                comboBoxArrival.Items.Add(display);
            }
            comboBoxDeparture.SelectedIndex = 1; // Free Flight'ı atla, ilk havalimanı seçili başlasın
            comboBoxArrival.SelectedIndex = 0;

            comboBoxDeparture.AutoCompleteMode = AutoCompleteMode.None;
            comboBoxDeparture.AutoCompleteSource = AutoCompleteSource.None;
            comboBoxArrival.AutoCompleteMode = AutoCompleteMode.None;
            comboBoxArrival.AutoCompleteSource = AutoCompleteSource.None;

            // Departure'da Free Flight seçimini devre dışı bırak
            comboBoxDeparture.SelectedIndexChanged += (s, e) =>
            {
                if (comboBoxDeparture.SelectedIndex == 0)
                {
                    comboBoxDeparture.SelectedIndex = 1; // Free Flight seçilemez
                }
            };

            // Eventleri bağla
            comboBoxArrival.SelectedIndexChanged += comboBoxArrival_SelectedIndexChanged;
        }

        private void SetupMap()
        {
            // Artık PictureBox resmiyle ilgili işleme gerek yok
        }

        // Kayıt başlat
        private void btnStartRecording_Click(object sender, EventArgs e)
        {
            // Departure ve Arrival ICAO'larını al
            string depIcao = null, arrIcao = null;
            if (comboBoxDeparture.SelectedIndex > 0)
            {
                int airportIdx = comboBoxDeparture.SelectedIndex - 1;
                if (airportIdx >= 0 && airportIdx < airports.Count)
                    depIcao = airports[airportIdx].ICAO;
            }
            if (comboBoxArrival.SelectedIndex > 0)
            {
                int airportIdx = comboBoxArrival.SelectedIndex - 1;
                if (airportIdx >= 0 && airportIdx < airports.Count)
                    arrIcao = airports[airportIdx].ICAO;
            }
            flightRecorder.Start(lastTailNum, depIcao, arrIcao);
            textBox1.AppendText("Uçuş kaydı başlatıldı.\n");

            // Her uçuş başında checklist uyarısını tekrar aktif et
            checklistAnnounced = false;
            checklistTimer.Stop();
        }

        // Kayıt durdur
        private void btnStopRecording_Click(object sender, EventArgs e)
        {
            flightRecorder.Stop();
            textBox1.AppendText("Uçuş kaydı durduruldu.\n");
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV Dosyası|*.csv|JSON Dosyası|*.json";
                sfd.Title = "Uçuş kaydını kaydet";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (sfd.FileName.EndsWith(".csv"))
                            flightRecorder.SaveToCsv(sfd.FileName);
                        else
                            flightRecorder.SaveToJson(sfd.FileName);
                        MessageBox.Show("Kayıt başarıyla kaydedildi!", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Kayıt kaydedilemedi:\n" + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // Kayıt aç
        private void btnOpenRecording_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "CSV Dosyası|*.csv|JSON Dosyası|*.json";
                ofd.Title = "Kayıtlı uçuşu aç";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (ofd.FileName.EndsWith(".csv"))
                        {
                            replayRecords = new List<FlightRecord>();
                            replayDepartureICAO = null;
                            replayArrivalICAO = null;
                            var lines = File.ReadAllLines(ofd.FileName);
                            int dataStart = 0;
                            if (lines.Length > 0 && lines[0].StartsWith("#"))
                            {
                                // Meta satırı
                                var meta = lines[0].TrimStart('#').Split(',');
                                foreach (var part in meta)
                                {
                                    var kv = part.Split('=');
                                    if (kv.Length == 2)
                                    {
                                        if (kv[0].Trim() == "DepartureICAO") replayDepartureICAO = kv[1].Trim();
                                        if (kv[0].Trim() == "ArrivalICAO") replayArrivalICAO = kv[1].Trim();
                                    }
                                }
                                dataStart = 1;
                            }
                            // Başlık satırını atla
                            if (lines.Length > dataStart && lines[dataStart].StartsWith("Timestamp"))
                                dataStart++;
                            for (int i = dataStart; i < lines.Length; i++)
                            {
                                var parts = lines[i].Split(',');
                                if (parts.Length >= 6)
                                {
                                    double heading = 0;
                                    // Heading verisi varsa parse et, yoksa 0 olarak kalır
                                    if (parts.Length >= 7)
                                        double.TryParse(parts[6], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out heading);
                                    
                                    replayRecords.Add(new FlightRecord
                                    {
                                        Timestamp = DateTime.Parse(parts[0]),
                                        Latitude = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                                        Longitude = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                                        Altitude = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture),
                                        TailNumber = parts[4],
                                        OnGround = bool.Parse(parts[5]),
                                        Heading = heading
                                    });
                                }
                            }
                        }
                        else
                        {
                            var json = File.ReadAllText(ofd.FileName);
                            // JSON formatında DepartureICAO ve ArrivalICAO root objede olabilir
                            var doc = System.Text.Json.JsonDocument.Parse(json);
                            replayDepartureICAO = doc.RootElement.TryGetProperty("DepartureICAO", out var dep) ? dep.GetString() : null;
                            replayArrivalICAO = doc.RootElement.TryGetProperty("ArrivalICAO", out var arr) ? arr.GetString() : null;
                            if (doc.RootElement.TryGetProperty("Records", out var recs))
                            {
                                replayRecords = System.Text.Json.JsonSerializer.Deserialize<List<FlightRecord>>(recs.GetRawText());
                            }
                            else
                            {
                                replayRecords = System.Text.Json.JsonSerializer.Deserialize<List<FlightRecord>>(json);
                            }
                        }
                        textBox1.AppendText($"{replayRecords.Count} kayıt yüklendi.\n");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Kayıt yüklenemedi:\n" + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // Replay başlat
        private void btnReplay_Click(object sender, EventArgs e)
        {
            if (replayRecords == null || replayRecords.Count == 0)
            {
                MessageBox.Show("Önce bir kayıt açmalısınız!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            replayIndex = 0;
            if (replayTimer != null)
            {
                replayTimer.Stop();
                replayTimer.Dispose();
            }
            replayTimer = new Timer();
            // Temel hız: 500ms (1x), oranla çarp
            int interval = (int)(500 / replaySpeed);
            if (interval < 10) interval = 10;
            replayTimer.Interval = interval;
            replayTimer.Tick += (s, ev) =>
            {
                if (replayIndex >= replayRecords.Count)
                {
                    replayTimer.Stop();
                    textBox1.AppendText("Replay tamamlandı.\n");
                    return;
                }
                var rec = replayRecords[replayIndex++];
                // JS'ye gönder
                if (webView.CoreWebView2 != null)
                {
                    string script = $"window.updateAircraftPosition({rec.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {rec.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {rec.Heading.ToString(System.Globalization.CultureInfo.InvariantCulture)});";
                    webView.ExecuteScriptAsync(script);
                }
            };
            replayTimer.Start();
            textBox1.AppendText("Replay başladı.\n");

            // Kalkış-varış rotasını göster (CSV veya JSON'dan alınan ICAO'lar)
            string depIcao = replayDepartureICAO;
            string arrIcao = replayArrivalICAO;
            if (!string.IsNullOrEmpty(depIcao) && !string.IsNullOrEmpty(arrIcao))
            {
                var dep = airports.FirstOrDefault(a => a.ICAO == depIcao);
                var arr = airports.FirstOrDefault(a => a.ICAO == arrIcao);
                if (dep != null && arr != null)
                {
                    var jsPayload = new
                    {
                        airports = new[] {
                            new {
                                ICAO = dep.ICAO,
                                Name = dep.Name,
                                datum_lat = dep.Lat,
                                datum_lon = dep.Lon,
                                City = dep.City,
                                Country = dep.Country
                            },
                            new {
                                ICAO = arr.ICAO,
                                Name = arr.Name,
                                datum_lat = arr.Lat,
                                datum_lon = arr.Lon,
                                City = arr.City,
                                Country = arr.Country
                            }
                        }
                    };
                    string jsJson = System.Text.Json.JsonSerializer.Serialize(jsPayload);
                    string script = $"window.showSelectedAirportsAndRoute({jsJson});";
                    if (webView.CoreWebView2 != null)
                        webView.ExecuteScriptAsync(script);
                }
            }
        }

        // Pause replay
        private void btnPauseReplay_Click(object sender, EventArgs e)
        {
            if (replayTimer != null && replayTimer.Enabled)
            {
                replayTimer.Stop();
                textBox1.AppendText("Replay duraklatıldı.\n");
            }
        }

        // Resume replay
        private void btnResumeReplay_Click(object sender, EventArgs e)
        {
            if (replayTimer != null && !replayTimer.Enabled)
            {
                replayTimer.Start();
                textBox1.AppendText("Replay devam ediyor.\n");
            }
        }

        // TrackBar hız değişimi
        private void trackBarReplaySpeed_ValueChanged(object sender, EventArgs e)
        {
            replaySpeed = trackBarReplaySpeed.Value + 1;
            if (lblReplaySpeed != null)
                lblReplaySpeed.Text = $"{replaySpeed:0.##}x";
            if (replayTimer != null)
            {
                // Temel hız: 500ms (1x), oranla çarp
                int interval = (int)(500 / replaySpeed);
                if (interval < 10) interval = 10;
                replayTimer.Interval = interval;
            }
        }

        private async void StartUdpListener()
        {
            udpService.DataReceived += (message) =>
            {
                Invoke(new Action(() =>
                {
                    // Mesajı parse et: eklenti 32 alan gönderir (lat,lon,elev,tailnum,onground,...,heading[23],...,airspeed[29],model[30],agl[31]).
                    // 49001 portuna X-Plane'in kendi UDP paketleri (DREF vb.) de düşebilir; bu biçime uymayanlar yok sayılır.
                    var parts = message.Split(',');
                    if (parts.Length >= PluginFieldCount &&
                        double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
                        double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lon))
                    {
                        double elev = 0;
                        if (parts.Length > 2)
                            double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out elev);
                        string tailnum = parts.Length > 3 ? parts[3] : "";
                        bool onground = false;
                        if (parts.Length > 4)
                        {
                            // 0 veya 1 olarak geliyorsa:
                            if (parts[4] == "1")
                                onground = true;
                            else if (parts[4] == "0")
                                onground = false;
                            // Alternatif olarak:
                            // int ongroundInt;
                            // if (int.TryParse(parts[4], out ongroundInt))
                            //     onground = (ongroundInt == 1);
                        }
                        double.TryParse(parts[HeadingIndex], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double heading);
                        double.TryParse(parts[AirspeedIndex], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double airspeed);
                        double.TryParse(parts[AglIndex], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double agl);

                        // Veri kutusuna saniyede bir okunabilir özet yaz (eklenti saniyede 10 mesaj gönderir)
                        if ((DateTime.Now - lastDataLogTime).TotalSeconds >= 1)
                        {
                            lastDataLogTime = DateTime.Now;
                            string state = onground ? "yerde" : FormattableString.Invariant($"AGL {agl:F0} m");
                            AppendLog(FormattableString.Invariant($"{tailnum}  {lat:F4}, {lon:F4}  HDG {heading:F0}°  {airspeed:F0} kt  {state}"));
                        }

                        lastTailNum = tailnum;
                        // JS'ye gönder
                        if (webView.CoreWebView2 != null)
                        {
                            string script = $"window.updateAircraftPosition({lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {heading.ToString(System.Globalization.CultureInfo.InvariantCulture)});";
                            webView.ExecuteScriptAsync(script);
                        }
                        // Kayıt aktifse ekle
                        if (flightRecorder.IsRecording)
                        {
                            flightRecorder.AddRecord(lat, lon, elev, tailnum, onground, heading);
                        }

                        // onground değişkeni: true/false
                        if (isFirstUdp)
                        {
                            lastOnGround = onground;
                            isFirstUdp = false;
                            // Checklist timer'ı başlat (sadece bir kez)
                            if (!checklistAnnounced)
                            {
                                checklistTimer.Stop();
                                checklistTimer.Start();
                            }
                        }
                        else
                        {
                            if (!onground && lastOnGround) // Yerden yeni kalktıysa
                            {
                                speechSynth.SpeakAsync("Aircraft has left the ground, retract the landing gear.");
                            }
                            lastOnGround = onground;
                        }
                    }
                }));
            };

            await udpService.StartListening(49001);
        }

        // Veri kutusunu sınırlı tutarak satır ekler (uzun uçuşlarda metin sınırsız büyümesin)
        private void AppendLog(string line)
        {
            if (textBox1.TextLength > 20000)
                textBox1.Text = textBox1.Text.Substring(textBox1.TextLength - 10000);
            textBox1.AppendText(line + Environment.NewLine);
        }

        // Event Handlers
        private void comboBoxDeparture_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxDeparture.SelectedIndex == 0)
            {
                // Free Flight seçili
            }
            else if (comboBoxDeparture.SelectedIndex > 0)
            {
                int airportIdx = comboBoxDeparture.SelectedIndex - 1;
                if (airportIdx >= 0 && airportIdx < airports.Count)
                {
                    string selectedIcao = airports[airportIdx].ICAO;
                }
            }
        }

        private void comboBoxArrival_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxArrival.SelectedIndex == 0)
            {
                // Free Flight seçili
            }
            else if (comboBoxArrival.SelectedIndex > 0)
            {
                int airportIdx = comboBoxArrival.SelectedIndex - 1;
                if (airportIdx >= 0 && airportIdx < airports.Count)
                {
                    string selectedIcao = airports[airportIdx].ICAO;
                }
            }
        }

        // Mouse eventleri ve PictureBox ile ilgili fonksiyonlar kaldırıldı

        private void RedrawTimer_Tick(object sender, EventArgs e)
        {
            redrawTimer.Stop();
            RedrawMap();
        }

        // Haritayı yeniden çiz
        private void RedrawMap()
        {
            // Artık PictureBox ile harita çizimi yok
        }

        // Test butonu için event
        private void TestButton_Click(object sender, EventArgs e)
        {
            // Test havalimanları - referans koordinatlar
            var testAirports = new List<Airport>
            {
                new Airport { ICAO = "LTFM", Name = "Istanbul Airport", Lat = "41.275278", Lon = "28.751944" },
                new Airport { ICAO = "OMDB", Name = "Dubai Intl", Lat = "25.252777778", Lon = "55.364444444" },
                new Airport { ICAO = "KATL", Name = "Atlanta Hartsfield", Lat = "33.6367", Lon = "-84.427863889" },
                new Airport { ICAO = "EGLL", Name = "London Heathrow", Lat = "51.4775", Lon = "-0.461388889" },
                new Airport { ICAO = "RKSI", Name = "Seoul Incheon", Lat = "37.462500000", Lon = "126.439166667" }
            };

        }

        // Zoom butonları için eventler kaldırıldı

        // Start Flight butonu için event
        private void StartFlightButton_Click(object sender, EventArgs e)
        {
            // Seçili havalimanlarını al
            Airport departureAirport = null;
            Airport arrivalAirport = null;

            if (comboBoxDeparture.SelectedIndex > 0)
            {
                int airportIdx = comboBoxDeparture.SelectedIndex - 1;
                if (airportIdx >= 0 && airportIdx < airports.Count)
                {
                    departureAirport = airports[airportIdx];
                }
            }

            if (comboBoxArrival.SelectedIndex > 0)
            {
                int airportIdx = comboBoxArrival.SelectedIndex - 1;
                if (airportIdx >= 0 && airportIdx < airports.Count)
                {
                    arrivalAirport = airports[airportIdx];
                }
            }

            // JS'ye gönderilecek payload
            var selectedList = new List<Airport>();
            if (departureAirport != null)
                selectedList.Add(departureAirport);
            if (arrivalAirport != null && arrivalAirport != departureAirport)
                selectedList.Add(arrivalAirport);

            var jsPayload = new
            {
                airports = selectedList.Select(a => new {
                    ICAO = a.ICAO,
                    Name = a.Name,
                    datum_lat = a.Lat,
                    datum_lon = a.Lon,
                    City = a.City,
                    Country = a.Country
                }).ToList()
            };
            string jsJson = JsonSerializer.Serialize(jsPayload);
            string script = $"window.showSelectedAirportsAndRoute({jsJson});";
            if (webView.CoreWebView2 != null)
                webView.ExecuteScriptAsync(script);

            // UI feedback
            if (departureAirport != null && arrivalAirport != null)
            {
                textBox1.AppendText($"Flight: {departureAirport.ICAO} -> {arrivalAirport.ICAO}\n");
            }
            else if (departureAirport != null)
            {
                textBox1.AppendText($"Flight: {departureAirport.ICAO} -> Free Flight\n");
            }
            else
            {
                textBox1.AppendText($"Free Flight\n");
            }
        }

        // Seçili havalimanları ile haritayı çiz
        private void RedrawMapWithSelectedAirports(List<Airport> selectedAirports)
        {
            // Artık PictureBox ile harita çizimi yok
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            // Artık PictureBox ile ilgili event yok
        }

        private void btnForward_Click(object sender, EventArgs e)
        {
            // 10 kayıt ileri sar (isteğe göre değiştirilebilir)
            if (replayRecords != null && replayRecords.Count > 0)
            {
                replayIndex += 10;
                if (replayIndex >= replayRecords.Count)
                    replayIndex = replayRecords.Count - 1;
                ShowReplayRecord();
            }
        }

        private void btnRewind_Click(object sender, EventArgs e)
        {
            // 10 kayıt geri sar (isteğe göre değiştirilebilir)
            if (replayRecords != null && replayRecords.Count > 0)
            {
                replayIndex -= 10;
                if (replayIndex < 0)
                    replayIndex = 0;
                ShowReplayRecord();
            }
        }

        // Seçili kaydı haritada gösteren yardımcı fonksiyon
        private void ShowReplayRecord()
        {
            if (replayRecords != null && replayIndex >= 0 && replayIndex < replayRecords.Count)
            {
                var rec = replayRecords[replayIndex];
                if (webView.CoreWebView2 != null)
                {
                    string script = $"window.updateAircraftPosition({rec.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {rec.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {rec.Heading.ToString(System.Globalization.CultureInfo.InvariantCulture)});";
                    webView.ExecuteScriptAsync(script);
                }
                textBox1.AppendText($"Replay: {replayIndex + 1}/{replayRecords.Count} - Zaman: {rec.Timestamp}\n");
            }
        }

        // Checklist timer tick event
        private void ChecklistTimer_Tick(object sender, EventArgs e)
        {
            checklistTimer.Stop();
            if (!checklistAnnounced)
            {
                speechSynth.SpeakAsync("Lütfen check listi kontrol ediniz");
                checklistAnnounced = true;
            }
        }
    }
}