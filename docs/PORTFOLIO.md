# Potok — engineering case study

## The problem

Display an iPhone on Windows with audio, using the phone's built-in Screen Mirroring UI. Support local Wi-Fi and a cable-based network path without a companion iOS app or Developer Mode.

## What this project implements

A native C# / WPF desktop application that manages an external AirPlay receiver, quality profiles, recovery from video negotiation errors, session diagnostics, and a phone-shaped video window. The window follows the incoming frame's aspect ratio and handles portrait/landscape changes. An alpha-composited vector frame sits above the native renderer without reading or copying video frames.

The AirPlay protocol and media pipelines come from **UxPlay / GStreamer**. Apple's software supplies device and USB-network support. These are deliberate component choices, not original protocol implementations attributed to this project.

## Technical decisions

| Challenge | Decision | Result / limit |
| --- | --- | --- |
| No iOS companion app | Use native Screen Mirroring with UxPlay | Verified on the test phone; new iOS releases may change compatibility |
| Cable-based connection | Use Apple's USB Personal Hotspot network | Working USB session; separate driver/network setup is required |
| Wireless connection | Advertise receiver on the local network | Working home-network session; no internet relay |
| GPU negotiation failure | Keep CPU decoding as the default and detect video errors | Stable fallback on the test PC; GPU mode remains experimental |
| Native video inside WPF | Reparent only the renderer belonging to our receiver process | Avoids desktop capture and an extra encoding step |
| Aliased curved window borders | Use a separate per-pixel-alpha vector layer above the native window | Rendering tests cover fractional alpha at multiple DPI scales |
| Honest connection status | Inspect the receiver's TCP route and parse iPhone telemetry | Shows USB vs LAN and sender-reported FPS, not measured end-to-end latency |
| First-run distribution | Self-contained app, separate opt-in upstream receiver download | SHA-256 pin; no third-party media bundle repackaged in our release |
| Phone input | Experimental BLE HID relative-mouse service and touchpad | Requires BLE peripheral support; live input not validated on compatible hardware |

## Validation

- Manual mirroring and audio tests with iPhone 17 / iOS 27 on Windows 10.
- Manual USB and local-network connections confirmed by the tester.
- Automated quality-profile, connection-route, telemetry and HID-report encoding checks.
- Native-window integration tests: attach, renderer resize recovery, clipping, detach, phone-window geometry and click-through alpha-overlay flags.
- Vector-frame tests at 100%, 125%, and 150% scale.

Windows 11 is a target platform, not a claim of completed real-device validation. Current work is an engineering preview, with one tested phone/PC configuration and ongoing visual feedback. No measured latency benchmark is advertised.

## Next milestones

- Broader Windows / iPhone / iOS test matrix.
- Validate BLE input using a compatible adapter and an iPhone.
- Save settings and improve first-run setup.
- Measure rendered FPS and end-to-end latency, then revisit hardware decoding.
- Signed installer and additional UI languages.
