-ESP32 Hardware Monitor Sender-

A lightweight Windows tray application written in C# that reads real-time system hardware data using LibreHardwareMonitor and sends it over Serial (USB) to an ESP32 for display.
Designed to auto-start on login and run silently in the system tray.

- Features

CPU temperature, power, clock speed, usage
GPU temperature, power, clock speed, usage, fan %
RAM usage
Disk free space (C, D, E)
Network upload/download speed
System uptime
CPU & GPU fan percentage (calculated from RPM)
Serial communication with ESP32
Runs as Windows system tray application
Auto-reconnect to COM port
Designed to run with Administrator privileges
Auto-start via Task Scheduler

- Technologies Used

C# (.NET Windows Desktop)
LibreHardwareMonitor
System.IO.Ports
Windows Forms (NotifyIcon)
Task Scheduler (for startup)
ESP32 (Arduino environment)
