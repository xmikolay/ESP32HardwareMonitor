using HidSharp.Reports;
using LibreHardwareMonitor.Hardware;
using System;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using System.Net.NetworkInformation;

namespace ESP32HardwareMonitor
{
    internal class Program
    {
        static string GetActiveWifiDescription()
        {
            var wifiAdapter = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up
                                   && ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

            return wifiAdapter?.Description;
        }

        static void Main(string[] args)
        {
            SerialPort port = new SerialPort("COM3", 115200);

            try
            {
                port.Open();
                Console.WriteLine("Connected to ESP32 on COM3");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening port: {ex.Message}");
                return;
            }

            Computer computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true
            };

            computer.Open();
            Console.WriteLine("Monitoring Hardware");

            while (true)
            {
                float cpuTemp = 0, cpuPower = 0, cpuClock = 0, cpuUsage = 0, cpuFan = 0;
                float gpuTemp = 0, gpuPower = 0, gpuClock = 0, gpuUsage = 0, gpuFan = 0;
                float ramUsage = 0;
                float wifiDown = 0, wifiUp = 0;

                float freeC = 0, freeD = 0, freeE = 0;

                const int CPU_FAN_MAX_RPM = 2250;
                const int GPU_FAN_MAX_RPM = 3500;

                string activeWifiName = GetActiveWifiDescription();

                //update all hardware sensors
                foreach (var hardware in computer.Hardware)
                {
                    hardware.Update();
                    //Console.WriteLine($"Found Hardware: {hardware.Name} Type: {hardware.HardwareType}");

                    //cpu
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            //temperature
                            if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && cpuTemp == 0)
                            {
                                cpuTemp = sensor.Value.Value;
                            }

                            //power
                            if (sensor.SensorType == SensorType.Power && sensor.Name.Contains("Package"))
                            {
                                cpuPower = sensor.Value ?? 0;
                            }

                            //clock
                            if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core"))
                            {
                                float clockValue = sensor.Value ?? 0;
                                if (clockValue > cpuClock) cpuClock = clockValue;
                            }

                            //usage
                            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("CPU Total"))
                            {
                                cpuUsage = sensor.Value ?? 0;
                            }
                        }
                    }

                    //gpu
                    if (hardware.HardwareType == HardwareType.GpuAmd)
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            //temperature
                            if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("GPU Core"))
                            {
                                gpuTemp = sensor.Value ?? 0;
                            }

                            //power
                            if (sensor.SensorType == SensorType.Power && sensor.Name.Contains("GPU Package"))
                            {
                                gpuPower = sensor.Value ?? 0;
                            }

                            //clock
                            if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("GPU Core"))
                            {
                                gpuClock = sensor.Value ?? 0;
                            }

                            //usage
                            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("GPU Core"))
                            {
                                gpuUsage = sensor.Value ?? 0;
                            }

                            //fan
                            if (sensor.SensorType == SensorType.Fan && sensor.Name.Contains("GPU Fan"))
                            {
                                gpuFan = sensor.Value ?? 0;
                            }
                        }
                    }

                    //get ram usage
                    if (hardware.HardwareType == HardwareType.Memory)
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Memory Used"))
                            {
                                ramUsage = sensor.Value ?? 0;
                            }
                        }
                    }

                    //get cpu fan from motherboard sensors
                    if (hardware.HardwareType == HardwareType.Motherboard)
                    {
                        foreach (var subhardware in hardware.SubHardware)
                        {
                            subhardware.Update();
                            foreach (var sensor in subhardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Fan && sensor.Name.Contains("CPU Fan"))
                                {
                                    cpuFan = sensor.Value ?? 0;
                                }
                            }
                        }
                    }

                    //use DriveInfo for disk info
                    try
                    {
                        //helper to compute free GB for a drive letter
                        static float GetFreeGb(string driveLetter)
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

                        freeC = GetFreeGb("C:\\");
                        freeD = GetFreeGb("D:\\");
                        freeE = GetFreeGb("E:\\");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"DriveInfo error: {ex.Message}");
                    }

                    //network throughput
                    if (hardware.HardwareType == HardwareType.Network)
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Throughput)
                            {
                                if (sensor.Name == "Download Speed")
                                {
                                    float val = ((sensor.Value ?? 0) * 8f) / 1024f / 1024f;
                                    if (val > wifiDown) wifiDown = val; //keep the highest value found
                                }

                                if (sensor.Name == "Upload Speed")
                                {
                                    float val = ((sensor.Value ?? 0) * 8f) / 1024f / 1024f;
                                    if (val > wifiUp) wifiUp = val; //keep the highest value found
                                }
                            }
                        }
                    }
                }           

                //convert fan RPM to percentage
                int cpuFanPercent = (int)((cpuFan / (float)CPU_FAN_MAX_RPM) * 100);
                int gpuFanPercent = (int)((gpuFan / (float)GPU_FAN_MAX_RPM) * 100);

                //clamp to 0-100% range
                cpuFanPercent = Math.Min(100, Math.Max(0, cpuFanPercent));
                gpuFanPercent = Math.Min(100, Math.Max(0, gpuFanPercent));

                TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

                string currentTime = DateTime.Now.ToString("HH:mm:ss");

                //build data string
                string data = $"CPU:{cpuTemp:F1},GPU:{gpuTemp:F1},RAM:{ramUsage:F1}," +
                             $"CPUPWR:{cpuPower:F0},CPUCLK:{cpuClock:F0},CPUUSE:{cpuUsage:F0},CPUFAN:{cpuFanPercent}," +
                             $"GPUPWR:{gpuPower:F0},GPUCLK:{gpuClock:F0},GPUUSE:{gpuUsage:F0},GPUFAN:{gpuFanPercent}," +
                             $"UPTIME:{(int)uptime.TotalSeconds}," +
                             $"DISKC:{freeC:F1},DISKD:{freeD:F1},DISKE:{freeE:F1}," +
                             $"WIFIDN:{wifiDown:F1},WIFIUP:{wifiUp:F1}\n";

                port.WriteLine(data);
                Console.WriteLine($"Sent: {data.TrimEnd()}");

                Thread.Sleep(500);
            }
        }
    }
}
