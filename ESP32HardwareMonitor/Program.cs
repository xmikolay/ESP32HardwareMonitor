using System;
using System.Threading;
using System.Windows.Forms;

namespace ESP32HardwareMonitor
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            //prevent multiple instances and dupicate tray icons by using a named mutex
            using var mutex = new Mutex(true, "ESP32HardwareMonitor_SINGLE_INSTANCE", out bool isNew);
            if (!isNew) return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayAppContext());
        }
    }
}