# Third-party software

Potok's C# / WPF integration and UI source is released under GPL-3.0-only. Copyright (c) 2026 Lill221. See LICENSE.

## UxPlay and uxplay-windows

Potok is a Windows interface and integration layer. It does **not** implement its own AirPlay protocol, Apple USB driver or video codec.

- UxPlay: https://github.com/FDH2/UxPlay — GPL-3.0.
- Windows receiver: https://github.com/leapbtw/uxplay-windows — GPL-3.0; source for the pinned release: https://github.com/leapbtw/uxplay-windows/tree/2.0.0.1736.
- Pinned Windows receiver archive: https://github.com/leapbtw/uxplay-windows/releases/download/2.0.0.1736/uxplay-windows.zip.
- Archive SHA-256: `9D3A51C15FC9DB857351195E7EB7BBB21700D9AE25D936A54BCF8536B62CCA18`.

The Potok release ZIP does **not** bundle or redistribute the receiver archive, GStreamer, Qt, FFmpeg, or Apple drivers. On first connection, the user may choose to download the pinned receiver directly from its upstream GitHub release. Potok checks the pinned SHA-256 before extraction. The upstream receiver's LICENSE.rtf remains in tools/uxplay after download; it describes its dependencies, including GStreamer, Qt, FFmpeg and mDNSResponder. Their respective licenses and source locations apply independently. This pin identifies a downloaded artifact, not a vendor signature or guarantee of its security.

## Microsoft .NET

The Windows x64 portable ZIP is self-contained and includes Microsoft .NET runtime files. .NET uses the MIT license and third-party notices:

- https://github.com/dotnet/runtime/blob/v8.0.33/LICENSE.TXT
- https://github.com/dotnet/runtime/blob/v8.0.33/THIRD-PARTY-NOTICES.TXT

Runtime and Windows Desktop licenses / notices are included under `licenses/` in the portable ZIP. The first release was validated with runtime 8.0.33. Future builds must keep notices aligned with their runtime packs.

## Apple and Bluetooth

Apple Devices and Apple USB/network drivers are installed separately by the user through Apple/Microsoft distribution channels. They are not included in Potok's downloads. iPhone, Dynamic Island, AirPlay, Windows and Bluetooth are trademarks of their respective owners; this independent project is not affiliated with or endorsed by Apple or Microsoft.
