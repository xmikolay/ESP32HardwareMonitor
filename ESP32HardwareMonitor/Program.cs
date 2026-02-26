using System.IO.Ports;
using System;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace ESP32HardwareMonitor
{
    internal class Program
    {
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
                IsMotherboardEnabled = true
            };

            computer.Open();
            Console.WriteLine("Monitoring Hardware");

            // Right after computer.Open(), add this:
            Console.WriteLine("\n=== ALL AVAILABLE FAN SENSORS ===");
            foreach (var hardware in computer.Hardware)
            {
                hardware.Update();

                // Check main hardware
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Fan)
                    {
                        Console.WriteLine($"{hardware.Name} - {sensor.Name}: {sensor.Value} RPM");
                    }
                }

                // Check subhardware (motherboard sensors)
                foreach (var subhardware in hardware.SubHardware)
                {
                    subhardware.Update();
                    foreach (var sensor in subhardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Fan)
                        {
                            Console.WriteLine($"{subhardware.Name} - {sensor.Name}: {sensor.Value} RPM");
                        }
                    }
                }
            }
            Console.WriteLine("=================================\n");

            while (true)
            {
                float cpuTemp = 0, cpuPower = 0, cpuClock = 0, cpuUsage = 0, cpuFan = 0;
                float gpuTemp = 0, gpuPower = 0, gpuClock = 0, gpuUsage = 0, gpuFan = 0;
                float ramUsage = 0;

                const int CPU_FAN_MAX_RPM = 2250;
                const int GPU_FAN_MAX_RPM = 3500;

                //update all hardware sensors
                foreach (var hardware in computer.Hardware)
                {
                    hardware.Update();

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

                            //fan
                            //if (sensor.SensorType == SensorType.Fan && sensor.Name.Contains("CPU"))
                            //{
                            //    cpuFan = sensor.Value ?? 0;
                            //}
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
                }

                //convert fan RPM to percentage
                int cpuFanPercent = (int)((cpuFan / (float)CPU_FAN_MAX_RPM) * 100);
                int gpuFanPercent = (int)((gpuFan / (float)GPU_FAN_MAX_RPM) * 100);

                //clamp to 0-100% range
                cpuFanPercent = Math.Min(100, Math.Max(0, cpuFanPercent));
                gpuFanPercent = Math.Min(100, Math.Max(0, gpuFanPercent));

                //build data string
                string data = $"CPU:{cpuTemp:F1},GPU:{gpuTemp:F1},RAM:{ramUsage:F1}," +
                             $"CPUPWR:{cpuPower:F0},CPUCLK:{cpuClock:F0},CPUUSE:{cpuUsage:F0},CPUFAN:{cpuFanPercent:F0}" +
                             $"GPUPWR:{gpuPower:F0},GPUCLK:{gpuClock:F0},GPUUSE:{gpuUsage:F0},GPUFAN:{gpuFanPercent:F0}\n";

                port.WriteLine(data);
                Console.WriteLine($"Sent: {data.TrimEnd()}");

                Thread.Sleep(1000);
            }
        }
    }
}
