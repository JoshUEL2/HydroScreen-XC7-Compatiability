using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using LibreHardwareMonitor.Hardware;
using Newtonsoft.Json;
using HidSharp;
using CorsairLink;
using CorsairLink.Devices;
using CorsairLink.Devices.CommanderCore;
using CorsairLink.Devices.HydroPlatinum;
using CorsairLink.Hid;
using CorsairLink.Synchronization;
using System.Security.Principal;

namespace SensorBridge
{
    class Program
    {
        static int APP_PORT = 14242;
        static int BRIDGE_PORT = 14243;
        static DateTime lastHeartbeat = DateTime.Now;
        static List<IDevice> corsairDevices = new List<IDevice>();
        static IDeviceGuardManager guardManager = null!;
        static ILogger logger = null!;

        static void Main(string[] args)
        {
            System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);

            Console.OutputEncoding = Encoding.UTF8;
            bool debugMode = args.Any(arg => arg.Equals("--debug", StringComparison.OrdinalIgnoreCase) || arg.Equals("-d", StringComparison.OrdinalIgnoreCase));
            logger = new FileLogger(debugMode);
            guardManager = new CorsairDevicesGuardManager();

            if (!IsAdministrator())
            {
                logger.Error("Startup", "NOT running as Administrator. Exiting immediately.");
                SendErrorAndExit("Administrator Access Required", 5); // Access Denied
                return;
            }

            logger.Info("Startup", "Running with Administrator privileges.");

            // 1. UDP Heartbeat Watchdog
            Task.Run(async () =>
            {
                var localEp = new IPEndPoint(IPAddress.Loopback, BRIDGE_PORT);
                using (var client = new UdpClient(localEp))
                {
                    while (true)
                    {
                        try
                        {
                            var result = await client.ReceiveAsync();
                            string msg = Encoding.UTF8.GetString(result.Buffer);
                            if (msg.Trim() == "ping") lastHeartbeat = DateTime.Now;
                        }
                        catch { }
                    }
                }
            });

            // Self-termination watchdog.
            Task.Run(async () =>
            {
                await Task.Delay(10000);
                while (true)
                {
                    if ((DateTime.Now - lastHeartbeat).TotalSeconds > 5)
                    {
                        Environment.Exit(0);
                    }
                    await Task.Delay(1000);
                }
            });

            Computer? computer = null;

            // Attempt to load the driver.
            try
            {
                computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMotherboardEnabled = true,
                    IsStorageEnabled = false,
                    IsControllerEnabled = true,
                    IsMemoryEnabled = true,
                    IsPsuEnabled = true
                };

                logger.Info("Startup", "Attempting to load LibreHardwareMonitor driver...");
                computer.Open();
                logger.Info("Startup", "LibreHardwareMonitor driver loaded successfully.");
            }
            catch (Exception ex)
            {
                logger.Error("Startup", "Failed to load LibreHardwareMonitor driver!", ex);
                SendErrorAndExit($"Driver Load Failed: {ex.Message}", 2); // File Not Found / Driver Error
                return;
            }

            // Initialize Corsair Devices.
            InitializeCorsairDevices();

            // 2. Main Loop (UDP Sender)
            using (var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                var endPoint = new IPEndPoint(IPAddress.Loopback, APP_PORT);

                while (true)
                {
                    try
                    {
                        var hardwareList = new List<JsonHardware>();

                        if (computer != null)
                        {
                            foreach (var hardware in computer.Hardware) hardware.Update();
                        
                            // 1. Get LHM Data.
                            hardwareList.AddRange(computer.Hardware.Select(h => new JsonHardware
                            {
                                Id = h.Identifier.ToString(),
                                Name = h.Name,
                                Type = h.HardwareType.ToString().Replace("Cpu", "CPU").Replace("GpuNvidia", "GPU").Replace("GpuAmd", "GPU"),
                                Sensors = h.Sensors.Select(s => new JsonSensor
                                {
                                    Id = s.Identifier.ToString(),
                                    Name = s.Name,
                                    Type = s.SensorType.ToString(),
                                    Value = s.Value ?? 0
                                }).ToList()
                            }));
                        }

                        // 2. Refresh & Get Corsair Data.
                        foreach (var device in corsairDevices)
                            {
                                try
                                {
                                    device.Refresh(); 
                                    
                                    var sensors = new List<JsonSensor>();
                                    
                                    logger.Debug("MainLoop", $"{device.Name}: Found {device.TemperatureSensors.Count} temp sensors, {device.SpeedSensors.Count} speed sensors");

                                    foreach(var temp in device.TemperatureSensors)
                                    {
                                        if (temp.TemperatureCelsius.HasValue)
                                        {
                                            logger.Debug("MainLoop", $"Adding Temp: {temp.Name} = {temp.TemperatureCelsius.Value}");
                                            sensors.Add(new JsonSensor 
                                            { 
                                                Id = $"{device.UniqueId}-temp-{temp.Channel}",
                                                Name = temp.Name, 
                                                Type = "Temperature", 
                                                Value = temp.TemperatureCelsius.Value 
                                            });
                                        }
                                    }
                                    
                                    foreach(var fan in device.SpeedSensors)
                                    {
                                        if (fan.Rpm.HasValue)
                                        {
                                            logger.Debug("MainLoop", $"Adding Fan: {fan.Name} = {fan.Rpm.Value}");
                                            sensors.Add(new JsonSensor 
                                            { 
                                                Id = $"{device.UniqueId}-fan-{fan.Channel}",
                                                Name = fan.Name, 
                                                Type = "Fan", 
                                                Value = fan.Rpm.Value 
                                            });
                                        }
                                        else
                                        {
                                            logger.Debug("MainLoop", $"Skipping Fan {fan.Name} (No Value)");
                                        }
                                    }

                                    if (sensors.Count > 0)
                                    {
                                        hardwareList.Add(new JsonHardware
                                        {
                                            Id = device.UniqueId,
                                            Name = device.Name,
                                            Type = "Cooler",
                                            Sensors = sensors
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.Error("MainLoop", $"Error refreshing {device.Name}", ex);
                                }
                            }


                        string json = JsonConvert.SerializeObject(hardwareList, Formatting.None);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        sender.Send(bytes, bytes.Length, endPoint);
                    }
                    catch (Exception ex)
                    {
                         logger.Error("MainLoop", $"General Error", ex);
                    }

                    Thread.Sleep(1000);
                }
            }
        }

        static void SendErrorAndExit(string errorMsg, int exitCode)
        {
            try
            {
                using (var sender = new UdpClient())
                {
                    string json = $"{{\"error\": \"{errorMsg}\"}}";
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, APP_PORT));
                }
                Thread.Sleep(500); // Give it a moment to send
            }
            catch { }
            
            Environment.Exit(exitCode);
        }

        static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        static void InitializeCorsairDevices()
        {
            logger.Info("Init", "Scanning for Corsair devices using dynamic discovery...");
            
            try 
            {
                var supportedDevices = new List<IDevice>();
                
                // HID Devices
                try 
                {
                    logger.Info("Init", "Checking HID Devices...");
                    var hidDevices = HidDeviceManager.GetSupportedDevices(guardManager, logger, isPassive: true);
                    supportedDevices.AddRange(hidDevices);
                }
                catch (Exception ex)
                {
                    logger.Error("Init", "Error in HidDeviceManager", ex);
                }

                // SIUSB Devices
                try
                {
                    logger.Info("Init", "Checking SiUsbXpress Devices...");
                    var siDevices = SiUsbXpressDeviceManager.GetSupportedDevices(guardManager, logger, isPassive: true);
                    supportedDevices.AddRange(siDevices);
                }
                catch (Exception ex)
                {
                    logger.Error("Init", "Error in SiUsbXpressDeviceManager", ex);
                }

                foreach (var device in supportedDevices)
                {
                    try
                    {
                        if (device.Connect())
                        {
                            corsairDevices.Add(device);
                            logger.Info("Init", $"Connected to {device.Name} ({device.UniqueId})");
                        }
                        else
                        {
                            logger.Info("Init", $"Failed to connect to {device.Name} ({device.UniqueId})");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Init", $"Error connecting to {device.Name}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                 logger.Error("Init", "Fatal error during device initialization", ex);
            }
        }
    }

    public class FileLogger : ILogger
    {
        private readonly bool _debugEnabled;
        private readonly string? _logPath;
        private readonly object _lock = new object();

        public bool DebugEnabled => _debugEnabled;

        public FileLogger(bool debugEnabled)
        {
            _debugEnabled = debugEnabled;
            
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string logFolder = Path.Combine(appData, "com.hydroscreen.app", "logs");
            
            try
            {
                if (!Directory.Exists(logFolder)) Directory.CreateDirectory(logFolder);
                _logPath = Path.Combine(logFolder, "sensor-bridge.log");

                // Rotate old log
                if (File.Exists(_logPath))
                {
                    string oldPath = _logPath + ".old";
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                    File.Move(_logPath, oldPath);
                }

                Log($"--- Sensor Bridge Started (Debug: {_debugEnabled}) ---");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize logger: {ex.Message}");
                _logPath = null;
            }
        }

        void Log(string msg)
        {
            if (_logPath == null) return;

            try
            {
                lock (_lock)
                {
                    // Check size and rotate if > 10MB
                    try
                    {
                        var info = new FileInfo(_logPath);
                        if (info.Exists && info.Length > 10 * 1024 * 1024)
                        {
                            string oldPath = _logPath + ".old";
                            if (File.Exists(oldPath)) File.Delete(oldPath);
                            File.Move(_logPath, oldPath);
                        }
                    }
                    catch { }

                    File.AppendAllText(_logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {msg}\n");
                }
            }
            catch { }
        }

        public void Info(string category, string message) => Log($"[INFO] {category}: {message}");
        public void Error(string category, string message) => Log($"[ERROR] {category}: {message}");
        public void Error(string category, Exception exception) => Log($"[ERROR] {category}: {exception}");
        public void Error(string category, string message, Exception exception) => Log($"[ERROR] {category}: {message} - {exception}");
        public void Warning(string category, string message) => Log($"[WARN] {category}: {message}");
        public void Warning(string category, Exception exception) => Log($"[WARN] {category}: {exception}");
        public void Warning(string category, string message, Exception exception) => Log($"[WARN] {category}: {message} - {exception}");
        
        public void Debug(string category, string message) 
        {
            if (_debugEnabled) Log($"[DEBUG] {category}: {message}");
        }

        public void Debug(string category, Exception exception) 
        {
             if (_debugEnabled) Log($"[DEBUG] {category}: {exception}");
        }

        public void Debug(string category, string message, Exception exception) 
        {
             if (_debugEnabled) Log($"[DEBUG] {category}: {message} - {exception}");
        }

        public void Flush() { }
    }

    public class JsonHardware
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required string Type { get; set; }
        public required List<JsonSensor> Sensors { get; set; }
    }

    public class JsonSensor
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required string Type { get; set; }
        public float Value { get; set; }
    }
}