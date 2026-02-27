# ESP32 Hardware Monitor Sender

A lightweight Windows tray application written in C# that reads real-time system hardware data using LibreHardwareMonitor and sends it over Serial (USB) to an ESP32 for display.
Designed to auto-start on login and run silently in the system tray.

## Features
- CPU temperature, power, clock speed, usage
- GPU temperature, power, clock speed, usage, fan %
- RAM usage
- Disk free space (C, D, E)
- Network upload/download speed
- System uptime
- CPU & GPU fan percentage (calculated from RPM)
- Serial communication with ESP32
- Runs as Windows system tray application
- Auto-reconnect to COM port
- Designed to run with Administrator privileges
- Auto-start via Task Scheduler

</br>

## Technologies Used
- C# (.NET Windows Desktop)
- LibreHardwareMonitor
- System.IO.Ports
- Windows Forms (NotifyIcon)
- Task Scheduler (for startup)
- ESP32 (Arduino environment)

</br>

## How It Works
- LibreHardwareMonitor reads live sensor data.
- The application formats data into a structured serial string.
- Data is sent over COM3 (115200 baud by default).
- The ESP32 parses and renders the data on an attached display.

</br>

## Setup
### Requirements
- Windows 10/11
- .NET runtime (if not self-contained build)
- ESP32 connected via USB
- Administrator privileges

### Build
- Open solution in Visual Studio
- Set configuration to Release
- Publish as:
  - win-x64
  - Self-contained (recommended)

### Run on Startup (Recommended)
- Use Task Scheduler:
  - Trigger: At log on
  - Tick: Run with highest privileges
  - Set 10–30 second delay (recommended)

</br>

## Why Administrator Is Required
LibreHardwareMonitor requires elevated privileges to access certain low-level hardware sensors (especially CPU, Motherboard and fan sensors).

</br>

## Default Configuration
- COM Port: COM3
- Baud Rate: 115200
- Update Interval: 500ms

These can be modified directly in the source code.

</br>

## Related Project

<p align="center">
  <img src="https://github.com/user-attachments/assets/a2e7a57d-e466-47c5-ba3c-53161e08d156" width="250"/>
  <img src="https://github.com/user-attachments/assets/625cd988-ea6d-4387-bfa5-34ae804acc0e" width="250"/>
  <img src="https://github.com/user-attachments/assets/025bf55a-fc29-4061-ac0e-e88d6a4ff827" width="250"/>
</p>

This application pairs with a custom ESP32 dashboard display project that visualises real-time PC statistics on an external screen. 

</br>

## License
MIT License

## Author

**Mikolay M.**  
Computer Science Student | Software Development  

This project was built as part of a personal dashboard system combining Windows system telemetry with an ESP32 display.

GitHub: https://github.com/xmikolay
