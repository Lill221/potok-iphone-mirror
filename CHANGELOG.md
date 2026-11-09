# Changelog

## 0.3.2-preview

First public engineering preview.

- Native Windows UI for local iPhone screen mirroring with audio.
- USB-network and home-LAN/Wi-Fi sessions validated on the test setup.
- Adaptive phone window and a smooth alpha-composited vector frame.
- CPU compatibility decoding, quality profiles, experimental HEVC and GPU options.
- USB/LAN route detection and iPhone sender telemetry.
- Experimental BLE relative-mouse service with an adapter capability check.
- Self-contained Windows x64 package; optional hash-pinned upstream receiver download.
- Automated rendering, configuration, route, telemetry and native-window checks.

Known limits: one manually tested phone/PC configuration; Windows 11 and wider iOS compatibility need more testing. BLE input is not live-validated on a compatible adapter. Preview executable is unsigned. Profile values are requests, not measured output-quality guarantees.
