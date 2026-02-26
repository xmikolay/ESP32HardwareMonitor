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
                IsMemoryEnabled = true
            };

            computer.Open();
            Console.WriteLine("Monitoring Hardware");

            while (true)
            {
                float cpuTemp = 0;
                float gpuTemp = 0;
                float ramUsage = 0;

                //update all hardware sensors
                foreach (var hardware in computer.Hardware)
                {
                    hardware.Update();

                    //get cpu temps
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Package"))
                            {
                                cpuTemp = sensor.Value ?? 0;
                            }
                        }
                    }

                    //get gpu temps
                    if (hardware.HardwareType == HardwareType.GpuAmd)
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("GPU Core"))
                            {
                                gpuTemp = sensor.Value ?? 0;
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
                }
            }
        }
    }
}
