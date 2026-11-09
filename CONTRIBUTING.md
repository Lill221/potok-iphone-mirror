# Contributing

Use Windows 10 2004+ or Windows 11 x64 and the .NET 8 SDK. Build with `./scripts/build.ps1` or `dotnet build src/PhoneMirror/PhoneMirror.csproj -c Release`.

Run the published executable with `--self-test` for profile/route/telemetry/HID/vector-rendering checks and `--native-host-test` for native window integration checks. Test flags never start the receiver or install drivers. A real-device test is still required for AirPlay changes.

Do not commit logs, downloaded tools, runtime packages, screenshots of private phone content, credentials, or device identifiers. Use synthetic artwork for public demos. Describe the tested phone/iOS/Windows version and whether the session used USB or LAN when reporting a bug; redact logs before sharing.

The code is GPL-3.0-only. Keep upstream attribution and document changes that affect receiver configuration or first-run downloads.
