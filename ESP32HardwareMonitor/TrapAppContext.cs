using LibreHardwareMonitor.Hardware;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ESP32HardwareMonitor
{
    //runs with no windows, just a tray icon and background worker
    public class TrayAppContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task? _workerTask;

        //pre defined constants (adjust as needed, these just fit my needs)
        private const string ComPortName = "COM3";
        private const int BaudRate = 115200;

        private const int CPU_FAN_MAX_RPM = 2250;
        private const int GPU_FAN_MAX_RPM = 3500;

        public TrayAppContext()
        {
            //build tray menu
            var menu = new ContextMenuStrip();
            menu.Items.Add("Show status", null, (_, __) => ShowStatusBalloon());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, __) => ExitApp());

            _trayIcon = new NotifyIcon
            {
                Text = "ESP32 Hardware Monitor Sender",
                Icon = System.Drawing.SystemIcons.Application, //replace with custom .ico if you want
                ContextMenuStrip = menu,
                Visible = true
            };

            _trayIcon.DoubleClick += (_, __) => ShowStatusBalloon();

            //start the background worker (loop)
            _workerTask = Task.Run(() => WorkerLoop(_cts.Token));
        }

        private void ShowStatusBalloon() //windows notification
        {
            _trayIcon.ShowBalloonTip(
                1000,
                "ESP32 Monitor",
                $"Running. Sending stats on {ComPortName}.",
                ToolTipIcon.Info);
        }

        private void ExitApp()
        {
            _cts.Cancel();

            //give worker a moment to stop cleanly
            try { _workerTask?.Wait(1500); } catch { /* ignore */ }

            _trayIcon.Visible = false;
            _trayIcon.Dispose();

            Application.Exit();
        }

        //gets free GB on a drive, returns 0 if any error (like drive not present)
        private static float GetFreeGb(string driveLetter)
        {
            try
            {
                var di = new System.IO.DriveInfo(driveLetter);
                if (!di.IsReady) return 0f;

                double freeBytes = di.AvailableFreeSpace;
                return (float)(freeBytes / (1024.0 * 1024.0 * 1024.0));
            }
            catch
            {
                return 0f;
            }
        }

        //our main loop, runs until app exits. reads hardware data and sends to ESP32 via serial.
        private void WorkerLoop(CancellationToken token)
        {
            //setup LibreHardwareMonitor once
            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true
            };
            computer.Open();

            SerialPort? port = null;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    //ensure serial is connected (auto-reconnect)
                    if (port == null)
                    {
                        port = new SerialPort(ComPortName, BaudRate);
                    }

                    if (!port.IsOpen)
                    {
                        port.Open();
                        //show a one time notification when it reconnects
                        _trayIcon.ShowBalloonTip(800, "ESP32 Monitor", $"Connected on {ComPortName}", ToolTipIcon.Info);
                    }

                    //variables to hold our sensor data (reset each loop)
                    float cpuTemp = 0, cpuPower = 0, cpuClock = 0, cpuUsage = 0, cpuFan = 0;
                    float gpuTemp = 0, gpuPower = 0, gpuClock = 0, gpuUsage = 0, gpuFan = 0;
                    float ramUsage = 0;
                    float wifiDown = 0, wifiUp = 0;
                    float freeC = 0, freeD = 0, freeE = 0;

                    foreach (var hardware in computer.Hardware)
                    {
                        hardware.Update();

                        //CPU
                        if (hardware.HardwareType == HardwareType.Cpu)
                        {
                            foreach (var sensor in hardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && cpuTemp == 0)
                                    cpuTemp = sensor.Value.Value;

                                if (sensor.SensorType == SensorType.Power && sensor.Name.Contains("Package"))
                                    cpuPower = sensor.Value ?? 0;

                                if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core"))
                                {
                                    float clockValue = sensor.Value ?? 0;
                                    if (clockValue > cpuClock) cpuClock = clockValue;
                                }

                                if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("CPU Total"))
                                    cpuUsage = sensor.Value ?? 0;
                            }
                        }

                        //GPU (AMD)
                        if (hardware.HardwareType == HardwareType.GpuAmd)
                        {
                            foreach (var sensor in hardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("GPU Core"))
                                    gpuTemp = sensor.Value ?? 0;

                                if (sensor.SensorType == SensorType.Power && sensor.Name.Contains("GPU Package"))
                                    gpuPower = sensor.Value ?? 0;

                                if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("GPU Core"))
                                    gpuClock = sensor.Value ?? 0;

                                if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("GPU Core"))
                                    gpuUsage = sensor.Value ?? 0;

                                if (sensor.SensorType == SensorType.Fan && sensor.Name.Contains("GPU Fan"))
                                    gpuFan = sensor.Value ?? 0;
                            }
                        }

                        /*
                        if (hardware.HardwareType == HardwareType.GpuNvidia)
                        {
                            foreach (var sensor in hardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("GPU Core"))
                                    gpuTemp = sensor.Value ?? 0;

                                if (sensor.SensorType == SensorType.Power && sensor.Name.Contains("GPU Package"))
                                    gpuPower = sensor.Value ?? 0;

                                if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("GPU Core"))         //IF YOU HAVE AN NVIDIA GPU, USE THIS
                                    gpuClock = sensor.Value ?? 0;

                                if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("GPU Core"))
                                    gpuUsage = sensor.Value ?? 0;

                                if (sensor.SensorType == SensorType.Fan && sensor.Name.Contains("GPU Fan"))
                                    gpuFan = sensor.Value ?? 0;
                            }
                        }
                        */

                        //RAM usage
                        if (hardware.HardwareType == HardwareType.Memory)
                        {
                            foreach (var sensor in hardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Memory Used"))
                                    ramUsage = sensor.Value ?? 0;
                            }
                        }

                        //CPU fan from motherboard subhardware
                        if (hardware.HardwareType == HardwareType.Motherboard)
                        {
                            foreach (var subhardware in hardware.SubHardware)
                            {
                                subhardware.Update();
                                foreach (var sensor in subhardware.Sensors)
                                {
                                    if (sensor.SensorType == SensorType.Fan && sensor.Name.Contains("CPU Fan"))
                                        cpuFan = sensor.Value ?? 0;
                                }
                            }
                        }

                        //Network throughput
                        if (hardware.HardwareType == HardwareType.Network)
                        {
                            foreach (var sensor in hardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Throughput)
                                {
                                    if (sensor.Name == "Download Speed")
                                    {
                                        float val = ((sensor.Value ?? 0) * 8f) / 1024f / 1024f;
                                        if (val > wifiDown) wifiDown = val;
                                    }

                                    if (sensor.Name == "Upload Speed")
                                    {
                                        float val = ((sensor.Value ?? 0) * 8f) / 1024f / 1024f;
                                        if (val > wifiUp) wifiUp = val;
                                    }
                                }
                            }
                        }
                    }

                    //disk free space (check once per loop)
                    freeC = GetFreeGb("C:\\");
                    freeD = GetFreeGb("D:\\");
                    freeE = GetFreeGb("E:\\");

                    //fan % conversion
                    int cpuFanPercent = (int)((cpuFan / CPU_FAN_MAX_RPM) * 100);
                    int gpuFanPercent = (int)((gpuFan / GPU_FAN_MAX_RPM) * 100);

                    cpuFanPercent = Math.Min(100, Math.Max(0, cpuFanPercent));
                    gpuFanPercent = Math.Min(100, Math.Max(0, gpuFanPercent));

                    TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

                    //build data string, this is what gets sent to the ESP32. adjust format as needed, just keep it consistent with what your ESP32 code expects.
                    string data =
                        $"CPU:{cpuTemp:F1},GPU:{gpuTemp:F1},RAM:{ramUsage:F1}," +
                        $"CPUPWR:{cpuPower:F0},CPUCLK:{cpuClock:F0},CPUUSE:{cpuUsage:F0},CPUFAN:{cpuFanPercent}," +
                        $"GPUPWR:{gpuPower:F0},GPUCLK:{gpuClock:F0},GPUUSE:{gpuUsage:F0},GPUFAN:{gpuFanPercent}," +
                        $"UPTIME:{(int)uptime.TotalSeconds}," +
                        $"DISKC:{freeC:F1},DISKD:{freeD:F1},DISKE:{freeE:F1}," +
                        $"WIFIDN:{wifiDown:F1},WIFIUP:{wifiUp:F1}\n"; // \n is the endline character, important for the ESP32 to know when a full line of data is received

                    port.WriteLine(data);

                    //sleep 
                    if (token.WaitHandle.WaitOne(500)) break;
                }
                catch (Exception ex)
                {
                    //if serial dies (e.g. ESP32 unplugged), close and retry after a short delay
                    try { port?.Close(); } catch { /* ignore */ }

                    //wait a bit before retrying
                    if (token.WaitHandle.WaitOne(1500)) break;

                    Debug.WriteLine(ex);
                }
            }

            //cleanup
            try { port?.Close(); } catch { }
            port?.Dispose();
            computer.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _cts.Dispose();
                _trayIcon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}