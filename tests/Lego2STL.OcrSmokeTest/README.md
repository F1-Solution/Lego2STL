# OCR smoke test

Proves each mobile recogniser is wired correctly and reads real text. It does not prove OCR
accuracy against a genuine catalogue page — that stays `LabelReadingAccuracyTests`, gated on
Windows and the reference document.

Nothing here runs in CI. CI (the `mobile` job in `.github/workflows/package.yml`) only proves
these three targets compile; a person runs this project by hand, on a device, emulator or
simulator, before a release that claims mobile OCR works.

| Platform | Run | Read the result |
|---|---|---|
| Android | Deploy `Lego2STL.OcrSmokeTest` to an emulator or device from Visual Studio / `dotnet build -t:Run -f net10.0-android36.0` | On screen |
| iOS | Run in the Simulator from Visual Studio for Mac / Xcode, or `dotnet build -t:Run -f net10.0-ios26.0` | On screen |
| macOS | `dotnet run --project tests/Lego2STL.OcrSmokeTest -f net10.0-macos26.0` | On stdout |

A PASS reads the fixture's part number and colour code back; a FAIL names what went wrong,
including an exception, since the smoke test's own failure has to be as legible as a real
one.
