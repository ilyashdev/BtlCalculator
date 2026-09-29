# Hall of shame: how not to write a calculator

A full breakdown of the decisions in [microsoft/calculator](https://github.com/microsoft/calculator) that led to
[BTL Calculator](../README.md). The goal is simple: everyone who reads this should laugh, and then not repeat any of it.

[Русская версия](HALL-OF-SHAME.ru.md)

All quotes are verbatim, from commit `4fd3fc5` (August 25, 2026), under the MIT License. Every exhibit has a file path,
so you can check it. The facts are about the public code; the conclusions and jokes are the author's.

## Contents

**Part I. Architecture**

1. [Four languages in one calculator](#1-four-languages-in-one-calculator)
2. [UWP: a platform its own maker moved away from](#2-uwp-a-platform-its-own-maker-moved-away-from)
3. [C++/CX: a dialect replaced in 2016](#3-ccx-a-dialect-replaced-in-2016)
4. [A calculation engine that downloads currency rates](#4-a-calculation-engine-that-downloads-currency-rates)
5. [DirectX to draw a line](#5-directx-to-draw-a-line)
6. [An open-source calculator with a closed engine](#6-an-open-source-calculator-with-a-closed-engine)
7. [Two telemetry projects](#7-two-telemetry-projects)
8. [Four test projects, and the unit tests are apps](#8-four-test-projects-and-the-unit-tests-are-apps)

**Part II. Code**

9. [enum → int → enum](#9-enum--int--enum)
10. [Six kinds of "too complex"](#10-six-kinds-of-too-complex)
11. [A struct for the sake of a struct](#11-a-struct-for-the-sake-of-a-struct)
12. [A catch that never catches](#12-a-catch-that-never-catches)
13. [1995 math on macros](#13-1995-math-on-macros)
14. [goto](#14-goto)
15. [Calculator bugs live in the OS tracker](#15-calculator-bugs-live-in-the-os-tracker)
16. [Leftover TODOs from Visual Studio templates](#16-leftover-todos-from-visual-studio-templates)
17. [Magic numbers in the tests](#17-magic-numbers-in-the-tests)
18. [Giant files](#18-giant-files)

[Summary](#summary)

---

# Part I. Architecture

## 1. Four languages in one calculator

**Decision.** The calculator is written in C# and three kinds of C++:

| Project | Language | What it does |
|---|---|---|
| `Calculator` | C# (UWP) | windows and markup |
| `Calculator.ViewModels` | C# | screen logic |
| `CalcManager` | C++ | calculation engine |
| `CalcManager.Interop` | C++/WinRT | bridge between C++ and C# |
| `GraphControl` | C++/CX | graph control |
| `GraphingImpl` | C++ | graphing engine (mocks in the public code) |
| `TraceLogging` | C++ | telemetry |
| `TraceLogging.Managed` | C# | also telemetry |

Add four test projects, and that's 12 projects.

**Context.** This isn't ancient history, it's the current state. Until August 2026 the ViewModels were written in
C++/CX: the code had 62 `ref class`es, and 18 are left now. The move to C# (#2491) touched 263 files, with +22,362 and
−26,045 lines. That's nearly fifty thousand lines of changes to put the calculator's screen logic in the language it
should have been written in from the start. The graphs and the engine are still in C++.

**What's wrong.** Every language boundary is interop. Data crosses it as bare `int`s and strings (see
[9](#9-enum--int--enum) and [17](#17-magic-numbers-in-the-tests)). The build needs both C++ and C# toolchains, and to
understand one button you read three languages.

**Ours.** One language, C#. Four projects: the core, the app, the Linux app and the tests.

## 2. UWP: a platform its own maker moved away from

**Decision.** The app is an `AppContainerExe` on `Microsoft.NETCore.UniversalWindowsPlatform`.

**Context.** UWP was the main app model of Windows 10. Since then Microsoft has moved developers to the Windows App SDK
and WinUI 3, and UWP is in maintenance. The calculator, the model app that ships with Windows, stayed on it.

**What's wrong.** The calculator runs only on Windows and builds only with the UWP toolchain, and its tests have to be
UWP apps too (see [8](#8-four-test-projects-and-the-unit-tests-are-apps)).

**Ours.** .NET 10 and Avalonia: Windows, Linux, macOS and Android from one codebase.

## 3. C++/CX: a dialect replaced in 2016

**Decision.** The graph control is written in C++/CX: `ref class`, `^` pointers, `Platform::String^`.

```cpp
// GraphControl/Control/Grapher.h
Windows::UI::Xaml::DispatcherTimer ^ m_TracingTrackingTimer;
Windows::UI::Core::CoreCursor ^ m_cachedCursor;
```

**Context.** C++/CX is a language extension that only Microsoft's compiler understands. In 2016 Microsoft introduced
standard C++/WinRT and recommends it instead of C++/CX. The `CalcManager.Interop` bridge in this very repository is
already C++/WinRT. So the old way and the new way live side by side in one solution.

**What's wrong.** It's a non-standard language that nobody learns anymore, no other compiler understands, and Microsoft
itself recommends moving away from.

**Ours.** Plain C#.

## 4. A calculation engine that downloads currency rates

**Decision.** Asynchrony is built on PPL (`concurrency::task`), a Windows 8-era library. The funniest part is where it
lives: in the C++ calculation engine.

```cpp
// CalcManager/UnitConverter.cpp
task<pair<bool, wstring>> UnitConverter::RefreshCurrencyRatios()
{
    shared_ptr<ICurrencyConverterDataLoader> currencyDataLoader = GetCurrencyConverterDataLoader();
    task<bool> loadDataResult;
    if (currencyDataLoader != nullptr)
    {
        loadDataResult = currencyDataLoader->TryLoadDataFromWebOverrideAsync();
    }
```

**Context.** `CalcManager` is the core that's supposed to multiply numbers. Yet its interface has
`TryLoadDataFromWebAsync`, and it returns the result of a rate refresh as a `pair<bool, wstring>`: success, plus a
string with a timestamp. PPL is also used for plotting (`Grapher::TryPlotGraph`), for rendering
(`RenderMain::RunRenderPassAsync`) and in the test helpers.

**What's wrong.** The math core knows about network downloads. A result is described by a pair instead of a type. And
it all depends on an async library that coroutines replace in modern C++, and that `async`/`await` replaces in C#.

**Ours.** An app service on `HttpClient` downloads the rates, and the core knows nothing about the network. Asynchrony is
`async`/`await`.

## 5. DirectX to draw a line

**Decision.** `GraphControl/DirectX` holds `DeviceResources`, `RenderMain`, `NearestPointRenderer` and
`DirectXHelper`: 1,640 lines of C++ to put curves on the screen.

**Context.** These files grew out of the Visual Studio "DirectX app" template, and traces of the template are still there
(see [16](#16-leftover-todos-from-visual-studio-templates)). The closed engine draws the curves through Direct2D.

**What's wrong.** A low-level graphics API for 2D lines. It welds the calculator to Windows: the only way to port it is
to rewrite it.

**Ours.** A control that draws with Avalonia (Skia underneath), the same drawing code on every platform. Curves are built in the background with a
margin past the edges of the screen, so panning is smooth.

## 6. An open-source calculator with a closed engine

**Decision.** The public repository has mocks instead of the graphing engine, in `src/GraphingImpl/Mocks`.

```cpp
// GraphingImpl/Mocks/GraphRenderer.h
virtual HRESULT DrawD2D1(ID2D1Factory* /*pDirect2dFactory*/, ID2D1RenderTarget* /*pRenderTarget*/, bool& hasSomeMissingDataOut)
{
    hasSomeMissingDataOut = false;
    return S_OK;
}
```

The mock draws nothing and reports success. Its function analyzer is `return nullptr;`, and any expression you type
parses "successfully".

**Context.** Build the "open-source" calculator and the graphing mode shows an empty grid. The real engine exists only
in Microsoft's build, and that's the one that:

- thinks about `tan(10x)` for five seconds with the CPU busy, then answers "This function is too complex to graph";
- draws vertical lines through the poles of `tan(100x)` and puts the trace point on the x-axis with the label
  y = 556.69;
- also crashed the whole app while rendering, for the author.

**What's wrong.** The most complex and most broken part is closed, so the community can neither find its bugs nor fix
them.

*The author's theory, offered as a joke and not as a fact, is that the engine stays closed because hiding holes is
cheaper than fixing them. It's easy to disprove: publish the code.*

**Ours.** The engine is open: the parser, adaptive sampling, interval arithmetic and function analysis, with tests.
`tan(10x)` takes 11 ms.

## 7. Two telemetry projects

**Decision.** `TraceLogging` (C++) and `TraceLogging.Managed` (C#), plus 20 event types in `TraceLogger.cs`:

```csharp
// Calculator.ViewModels/Common/TraceLogger.cs
private const string EventNameButtonUsage = "ButtonUsageInSession";
private const string EventNameInputPasted = "InputPasted";
private const string EventNameVariableChanged = "VariableChanged";
private const string EventNameGraphTheme = "GraphTheme";
private const string EventNameConverterInputReceived = "ConverterInputReceived";
...
```

**Context.** `ButtonUsageInSession` is a counter: how many times you pressed each button, and in which mode. Separate
events go out when you paste text, change a graph variable or the graph theme, or type a value into a converter.

**What's wrong.** A calculator doesn't need to report how you calculate. And it has two separate projects in two
languages for it.

**Ours.** No telemetry. [PRIVACY.md](../PRIVACY.md) is shorter than this section.

## 8. Four test projects, and the unit tests are apps

**Decision.** `Calculator.Tests` (C#), `CalculatorUnitTests` (C++), `CalculatorUITests` and `CalculatorUITestFramework`.
Both *unit* test projects are UWP apps:

```xml
<!-- Calculator.Tests/Calculator.Tests.csproj -->
<OutputType>AppContainerExe</OutputType>

<!-- CalculatorUnitTests/CalculatorUnitTests.vcxproj -->
<AppContainerApplication>true</AppContainerApplication>
<ApplicationType>Windows Store</ApplicationType>
```

**Context.** The tests need the UI thread, so they have their own helper that waits for it with an `INFINITE` timeout:

```cpp
// CalculatorUnitTests/AsyncHelper.h
static void RunOnUIThread(std::function<void()>&& action, DWORD timeout = INFINITE);
```

**What's wrong.** To test that 2 + 2 = 4, you build and deploy a Windows Store app. These tests can't run on Linux or in a
plain CI container. And the graphing engine isn't tested at all, because it isn't in the repository.

**Ours.** The core is a plain .NET library with 249 xUnit tests, and the UI has 37 tests that run without a window; `dotnet test` works on any OS.

---

# Part II. Code

## 9. enum → int → enum

**Decision.** The graphs have proper error enums:

```cpp
// GraphControl/Models/Equation.h
public enum class ErrorType { Evaluation, Syntax, Abort, };
public enum class EvaluationErrorCode { ... };
public enum class SyntaxErrorCode { ... };
```

The graph control stores the error in two `int`s:

```cpp
// GraphControl/Control/Grapher.h
int m_errorType;
int m_errorCode;
```

**Context.** The engine fills them through `int&` out parameters:

```cpp
// GraphControl/Control/Grapher.cpp
if ((graphExpression = m_solver->ParseInput(request, m_errorCode, m_errorType)))
...
m_solver->HRErrorToErrorInfo(m_renderMain->GetRenderError(), m_errorCode, m_errorType);
...
eq->GraphErrorType = static_cast<ErrorType>(m_errorType);
eq->GraphErrorCode = m_errorCode;
```

On the way, the type becomes an enum again, but the code stays an `int`. Then the ViewModel uses the type to decide which
enum to cast the code to:

```csharp
// Calculator.ViewModels/GraphingCalculator/EquationViewModel.cs
public static string EquationErrorText(GraphControl.ErrorType errorType, int errorCode)
{
    if (errorType == GraphControl.ErrorType.Evaluation)
    {
        switch ((GraphControl.EvaluationErrorCode)errorCode)
```

And the test dutifully turns the enum into an `int` so the method can turn it back:

```csharp
// Calculator.Tests/GraphingViewModelTests.cs
EquationViewModel.EquationErrorText(
    GraphControl.ErrorType.Evaluation,
    (int)GraphControl.EvaluationErrorCode.Overflow));
```

**What's wrong.** The compiler can't check that the code matches the type. Pass a syntax error code with the type
`Evaluation`, and you get someone else's message without a single warning.

**Ours.**

```csharp
public sealed class GraphInputException(GraphInputError error) : Exception($"Invalid graph input: {error}.")
{
    public GraphInputError Error { get; } = error;
}
```

The error is an enum from start to finish.

## 10. Six kinds of "too complex"

**Decision.** In the same `switch`:

```csharp
case GraphControl.EvaluationErrorCode.TooComplexToSolve:
case GraphControl.EvaluationErrorCode.EquationTooComplexToSolve:
case GraphControl.EvaluationErrorCode.EquationTooComplexToSolveSymbolic:
case GraphControl.EvaluationErrorCode.EquationTooComplexToPlot:
case GraphControl.EvaluationErrorCode.InequalityTooComplexToSolve:
case GraphControl.EvaluationErrorCode.GE_TooComplexToSolve:
    return Resource("TooComplexToSolve");
```

| Code | Value |
|---|---:|
| `TooComplexToSolve` | 4 |
| `EquationTooComplexToSolveSymbolic` | −7 |
| `EquationTooComplexToSolve` | −9 |
| `EquationTooComplexToPlot` | −10 |
| `InequalityTooComplexToSolve` | −41 |
| `GE_TooComplexToSolve` | −506 |

**Context.** The values are all over the place: positive, negative, with gaps down to −506. They look like codes
collected over the years in different parts of the closed engine.

**What's wrong.** Six ways to give up and one message for the user. Judging by `tan(10x)`, it's the busiest path in the
code.

## 11. A struct for the sake of a struct

**Decision.**

```cpp
// CalcManager/CalculatorHistory.h
struct HISTORYITEMVECTOR
{
    std::shared_ptr<std::vector<std::pair<std::wstring, int>>> spTokens;
    std::shared_ptr<std::vector<std::shared_ptr<IExpressionCommand>>> spCommands;
    std::wstring expression;
    std::wstring result;
};

struct HISTORYITEM
{
    HISTORYITEMVECTOR historyItemVector;
};
```

**Context.** `HISTORYITEM` wraps a single field. An expression token is a `pair<wstring, int>`: a string plus an `int`
whose meaning the type doesn't tell you. The result of the calculation is stored as a string. The all-caps names and the
`sp` prefixes are 1990s Win32 Hungarian notation.

**What's wrong.** Two structs where one would do, anonymous pairs instead of types, and extra `shared_ptr`s around
collections.

**Ours.**

```csharp
public sealed record HistoryEntry(IReadOnlyList<ExpressionToken> Expression, BigDecimal Result);
```

## 12. A catch that never catches

**Decision.** A `std::vector`, wrapped in COM-style error codes:

```cpp
// CalcManager/CalculatorVector.h
ResultCode SetAt(_In_ unsigned int index, _In_opt_ TType item)
{
    try
    {
        m_vector[index] = item;
    }
    catch (const std::out_of_range& /*ex*/)
    {
        return E_BOUNDS;
    }
    return S_OK;
}
```

**Context.** Right next to it, `GetAt` gets it right with `m_vector.at(index)`, which does throw `out_of_range`. It looks
like `SetAt` was copied from it and `.at()` was replaced with `[]`. And `GetSize` returns a `ResultCode` that is always
`S_OK`:

```cpp
ResultCode GetSize(_Out_ unsigned int* size)
{
    *size = static_cast<unsigned>(m_vector.size());
    return S_OK;
}
```

**What's wrong.** `operator[]` doesn't check bounds and never throws. An index out of range is undefined behaviour here,
not `E_BOUNDS`. The wrapper around a standard container turns exceptions into HRESULTs and the size into an out
parameter.

## 13. 1995 math on macros

**Decision.** The heart of the calculator is `ratpak`, a rational number library:

```cpp
// CalcManager/Ratpack/ratpak.h
//  Package Title  ratpak
//  File           ratpak.h
//  Copyright      (C) 1995-99 Microsoft
//  Date           01-16-95
```

Memory is managed by hand, with 233 calls to `destroyrat`/`destroynum` across the engine. Copying a number is a macro:

```cpp
#define DUPRAT(a, b)            \
    destroyrat(a);              \
    createrat(a);               \
    DUPNUM((a)->pp, (b)->pp);   \
    DUPNUM((a)->pq, (b)->pq);
```

**Context.** It's a four-statement macro with no `do { } while (0)`. Write `if (x) DUPRAT(a, b);` and only the first line
is under the `if`: the number is destroyed conditionally, and the other three lines always run. It's a first-year
classic. The debug version of `createrat` writes every created number, with its file and line, to `OutputDebugString`.
That's how memory leaks were hunted in 1995.

Here it is at work in the arcsine:

```cpp
// CalcManager/Ratpack/itrans.cpp
PRAT pret = nullptr;
PRAT phack = nullptr;
int32_t sgn = SIGN(*px);

(*px)->pp->sign = 1;
(*px)->pq->sign = 1;

// Avoid the really bad part of the asin curve near +/-1.
DUPRAT(phack, *px);
_subrat(&phack, rat_one, precision);
```

The variable is called `phack`, and the comment suggests avoiding "the really bad part of the curve".

**What's wrong.** In 2026, the calculator's core rests on 30-year-old C-style code with manual memory management and
macros that break under a plain `if`.

**Ours.** `BigDecimal` and `BigMath` in C#: immutable numbers, a garbage collector, not a single macro.

## 14. goto

**Decision.**

```cpp
// CalcManager/CEngine/scicomm.cpp
        DoPrecedenceCheckAgain:
            ...
                    // Precedence Inversion Higher to lower can happen which needs explicit enclosure of brackets
                    ...
                    m_HistoryCollector.PopLastOpndStart();
                    goto DoPrecedenceCheckAgain;
```

**Context.** The label is on line 276 and the jump is on line 335. That's how the calculator handles operator precedence
in 2026. Next to it is a comment where the developers themselves describe a "precedence inversion" that has to be patched
over with extra brackets in the history.

**What's wrong.** A loop disguised as a `goto`, inside `CCalcEngine::ProcessCommandWorker`, a function 770 lines long
(111–881). Dijkstra wrote "Go To Statement Considered Harmful" in 1968.

## 15. Calculator bugs live in the OS tracker

**Decision.**

```csharp
// Calculator/Views/MainPage.xaml.cs
if (deferral == null)
{
    // FIXME: https://microsoft.visualstudio.com/DefaultCollection/OS/_workitems/edit/47775705/
    TraceLogger.GetInstance().LogRecallError("55e29ba5-6097-40ec-8960-458750be3039");
    return;
}
```

**Context.** The open-source code links to a **private** internal tracker, in the **OS** project. Instead of a fix, a
hard-coded GUID goes to telemetry. The calculator's bugs and the operating system's bugs share a pile.

And more:

```csharp
// Calculator/Views/DateCalculator.xaml.cs
// We choose 2550 as the max year because CalendarDatePicker experiences clipping
// issues just after 2558.  We would like 9999 but will need to wait for a platform
// fix before we use a higher max year.  This platform issue is tracked by
// TODO: MSFT-9273247
private const int c_maxYear = 2550;
```

The date calculator can't go past the year 2550, because Microsoft's **own** date picker gets clipped after 2558. The
calculator is waiting for a platform fix. The platform is also Microsoft.

```csharp
// Calculator/App.xaml.cs
// TODO: MSFT 14645325: Set this directly from XAML.
// Currently this is bugged so the property is only respected from code-behind.
HighContrastAdjustment = ApplicationHighContrastAdjustment.None;
```

A bug in their own XAML, worked around in code.

**What's wrong.** If this is the code Microsoft isn't embarrassed to show, and its bugs sit in the OS tracker, we can
only guess what the code it doesn't show looks like. We don't know. We're just asking.

## 16. Leftover TODOs from Visual Studio templates

**Decision.**

```cpp
// GraphControl/DirectX/RenderMain.cpp
// Updates application state when the window size changes (e.g. device orientation change)
void RenderMain::CreateWindowSizeDependentResources()
{
    // TODO: Replace this with the sizedependent initialization of your app's content.
    RunRenderPass();
```

```cpp
// CalculatorUnitTests/UnitTestApp.xaml.cpp
if (e->PreviousExecutionState == ApplicationExecutionState::Terminated)
{
    // TODO: Restore the saved session state only when appropriate, scheduling the
    // final launch steps after the restore is complete
}
...
// TODO: Save application state and stop any background activity
```

**Context.** These aren't the developers' notes. They're text from Visual Studio project templates: "DirectX App" and
"Blank App (UWP)". A template leaves these TODOs for the developer to replace.

**What's wrong.** The Windows calculator's graph renderer started as "File → New Project", and nobody ever replaced the
placeholder. The empty `if` with a TODO in the test app has been there since January 2019, the first days of the
public repository.

## 17. Magic numbers in the tests

**Decision.**

```csharp
// Calculator.Tests/HistoryTests.cs
// CalcManager command IDs used by the interop boundary.
private const int CommandNULL = 0;
private const int CommandSIGN = 80;
private const int CommandCLEAR = 81;
private const int CommandCENTR = 82;
private const int CommandBACK = 83;
private const int CommandDIV = 91;
private const int CommandMUL = 92;
private const int CommandADD = 93;
private const int CommandSUB = 94;
private const int CommandSIN = 102;
private const int CommandCOS = 103;
private const int CommandTAN = 104;
...
```

**Context.** Calculator commands cross the C++/C# boundary as bare `int`s (see
[1](#1-four-languages-in-one-calculator)). There's no shared definition, so the test declares the numbers again, by
hand.

**What's wrong.** If a number changes in C++, the test quietly starts testing something else. Catching exactly that is
what tests are for.

## 18. Giant files

```
Original                                         lines
Calculator/App.xaml                     ████████████████████████▌ 2444
StandardCalculatorViewModel.cs          ████████████████████▋     2060
CalcManager/Ratpack/conv.cpp            ███████████████▍          1533
UnitConverterViewModel.cs               ███████████████▎          1525
GraphingNumPad.xaml                     ██████████████▊           1474
Calculator.xaml                         ██████████████            1406

BTL Calculator
BigMath.cs (all the exact math)         █████████▊                 978
FunctionAnalyzer.cs (all the analysis)  █████████▋                 967
CurveBuilder.cs (curve plotting)        ███████▌                   755
```

**Context.** `App.xaml` holds the app's resources and styles, 2,444 lines. `StandardCalculatorViewModel.cs` is a single
ViewModel of 2,060 lines.

**What's wrong.** The original's styles alone are longer than all of our exact arithmetic, with its trigonometry,
logarithms, hyperbolic functions and gamma function.

---

## Summary

| | Original | BTL Calculator |
|---|---|---|
| Languages | C#, C++, C++/CX, C++/WinRT | C# |
| Projects | 12 | 4 |
| Platforms | Windows | Windows, Linux, Android, iOS, macOS |
| Graphing engine | closed, mocks in the public code | open, with tests |
| `tan(10x)` | "Too complex to graph" after 5 s | 11 ms |
| Tests | 4 projects; the unit tests are UWP apps | 249 core and 37 UI tests; `dotnet test` on any OS |
| Telemetry | 2 projects, 20 event types | none |
| Size | ≈ 77,000 lines (26k C++, 36k C#, 15k XAML) without the engine | 18,723 lines with the engine and the tests |

It's a calculator. It takes numbers and gives numbers back. We rewrote it to show that it can be done simply, and to make
you laugh. We hope we managed both.

*Not affiliated with, endorsed by or sponsored by Microsoft. "Windows" and "Microsoft" are trademarks of Microsoft
Corporation. Code snippets are quoted from [microsoft/calculator](https://github.com/microsoft/calculator) under the MIT
License.*
