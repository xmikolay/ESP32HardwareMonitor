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
        }
    }
}
