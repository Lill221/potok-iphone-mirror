# Start here / Быстрый запуск

1. Extract the entire ZIP into a writable folder. Do not launch the EXE inside the ZIP.
2. Open **PhoneMirror.exe**. The .NET runtime is included; a developer SDK is not needed.
3. Click **Начать трансляцию**. On first run, accept the optional UxPlay receiver download (about 110 MB, from its upstream GitHub release). Internet access is needed for this first download. Initial discovery-service setup may ask for administrator approval.
4. On iPhone, open **Control Center → Screen Mirroring → Potok**.

## Wi-Fi / локальная сеть

Connect iPhone and PC to the same router; the PC may use Ethernet. For a wireless-only test, disconnect USB and turn off Personal Hotspot on iPhone. Allow the receiver through Windows Firewall for your trusted local network. Guest Wi-Fi / client isolation can prevent discovery.

## USB

Install Apple's **Apple Devices** app separately. Connect and unlock iPhone, accept **Trust This Computer**, and enable **Personal Hotspot**. Windows must show an active **Apple Mobile Device Ethernet** network adapter. This project uses AirPlay over the USB network; it is not a raw USB video-capture driver. Personal Hotspot availability depends on device/carrier configuration.

## Image quality

Start with **1080p / 60 FPS** and **CPU compatibility**. Higher-detail HEVC profiles are experimental. Profile numbers are requests to iPhone, not guaranteed output resolution/FPS. Reconnect Screen Mirroring after changing a profile.

## Mouse

Experimental and hardware-dependent. Windows must report BLE peripheral support on its Bluetooth adapter; AssistiveTouch and user pairing on iPhone are required. The built-in checker explains if the adapter is incompatible. Mouse control is not a verified general-release feature yet.

## Phone window

Drag by the frame. Right-click the frame for Always on top and the optional decorative Dynamic Island. The real iPhone island can already be part of the stream. Resize from the edge; F11 enlarges to the work area; Escape closes the phone window. The main app stays open.

## Help

Open **Диагностика** to view local logs. They may contain device names and network addresses: redact them before posting publicly. If a download hash check fails, Potok stops; do not replace the expected hash to bypass the check. See README.ru.md for Russian documentation and README.md for the project overview.
