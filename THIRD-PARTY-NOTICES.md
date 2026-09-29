# Third-party notices

BTL Calculator uses the following components and data. Their licenses apply to them, not to the rest of the project.

## Fluent UI System Icons

The icon font `src/Calculator.UI/Assets/Fonts/FluentSystemIcons-Regular.ttf`.
Source: https://github.com/microsoft/fluentui-system-icons — MIT License, Copyright (c) 2020 Microsoft Corporation.
The full license text is in `src/Calculator.UI/Assets/Fonts/FluentSystemIcons-LICENSE.txt`.

## Unicode CLDR

Currency names in `src/Calculator.UI/Resources/Currency/CurrencyNames.json` come from the Unicode Common Locale Data
Repository (https://github.com/unicode-org/cldr-json), created by `tools/Import-CurrencyNames.ps1`.
Unicode License v3 — Copyright © 1991-2024 Unicode, Inc. See https://www.unicode.org/license.txt.
## ExchangeRate-API

Currency rates are downloaded at run time from the open endpoint of ExchangeRate-API (https://open.er-api.com).
Its terms require the attribution "Rates By Exchange Rate API" with a link, which the currency converter shows.
See https://www.exchangerate-api.com/terms.

## .NET

The app is built on .NET (https://github.com/dotnet/runtime) — MIT License, Copyright (c) .NET Foundation and
Contributors.

## Avalonia

The user interface runs on Avalonia (https://github.com/AvaloniaUI/Avalonia) — MIT License, Copyright (c) The
Avalonia Project. Avalonia draws with SkiaSharp (https://github.com/mono/SkiaSharp, MIT License) and HarfBuzzSharp
(MIT License), which include Skia (BSD 3-Clause License, Copyright (c) Google Inc.) and HarfBuzz (Old MIT License).
## Development only

The tests use xUnit (https://github.com/xunit/xunit, Apache License 2.0) and Avalonia.Headless. They are not part of the
app.

## Fonts of the operating system

On Windows the app uses the system fonts Segoe UI and Cambria Math. They are not distributed with the app.
