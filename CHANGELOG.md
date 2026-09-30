# Changelog

## Unreleased

- The app logo: window and taskbar icon, the .exe icon, the Android launcher icon (adaptive), About in Settings.
- A link to the project on GitHub in About.
- The installed app is named "Calculator" (launcher, Start menu, window title); About still shows BTL Calculator.
- Releases: the Android APK is signed with the project's release key; a Microsoft Store package (MSIX bundle for x64
  and arm64) is built once the Store identity is set up.

## 0.1.0 — first public version

The whole calculator, rewritten from scratch in C# on Avalonia.

- Standard, scientific, programmer and date calculation modes; history and memory.
- Arbitrary-precision decimal arithmetic with exact angles and the gamma function.
- Graphing with its own engine: explicit and implicit curves, inequalities, parameters, adaptive sampling with interval
  arithmetic, function analysis including periodic functions, tracing, sharing.
- 12 unit converters and a currency converter with rates from ExchangeRate-API (cached offline).
- 7 languages (English, Russian, Chinese, Hindi, Spanish, Arabic, French) with plural forms and right-to-left layout.
- Windows 11 look: the Mica window material, Fluent transitions, light and dark themes, "keep on top".
- Windows, Linux (one Native AOT binary) and macOS from one desktop project, and Android: the back gesture, pinch zoom
  of graphs. The system share sheet on Windows and Android.
- GitHub Actions: tests on Windows, Linux and macOS for every push; a version tag publishes Windows and Linux binaries
  (x64 and arm64) and the Android APK.
