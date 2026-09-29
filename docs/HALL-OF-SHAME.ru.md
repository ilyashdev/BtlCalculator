# Зал славы: как не надо писать калькулятор

Полный разбор решений в [microsoft/calculator](https://github.com/microsoft/calculator), из-за которых появился
[BTL Calculator](../README.ru.md). Цель простая: чтобы каждый, кто это прочитает, посмеялся, а потом не повторил это
у себя.

[English version](HALL-OF-SHAME.md)

Все цитаты дословные и взяты из коммита `4fd3fc5` (25 августа 2026) под лицензией MIT. У каждого экспоната указан путь
к файлу, так что всё можно проверить. Факты относятся к открытому коду, выводы и шутки принадлежат автору.

## Содержание

**Часть I. Архитектурные решения**

1. [Четыре языка в одном калькуляторе](#1-четыре-языка-в-одном-калькуляторе)
2. [UWP: платформа, от которой ушли сами](#2-uwp-платформа-от-которой-ушли-сами)
3. [C++/CX: диалект, заменённый в 2016 году](#3-ccx-диалект-заменённый-в-2016-году)
4. [Движок вычислений, который загружает курсы валют](#4-движок-вычислений-который-загружает-курсы-валют)
5. [DirectX, чтобы нарисовать линию](#5-directx-чтобы-нарисовать-линию)
6. [Открытый калькулятор с закрытым движком](#6-открытый-калькулятор-с-закрытым-движком)
7. [Два проекта телеметрии](#7-два-проекта-телеметрии)
8. [Четыре проекта тестов, и юнит-тесты — это приложения](#8-четыре-проекта-тестов-и-юнит-тесты--это-приложения)

**Часть II. Код**

9. [enum → int → enum](#9-enum--int--enum)
10. [Шесть видов «слишком сложно»](#10-шесть-видов-слишком-сложно)
11. [Структура ради структуры](#11-структура-ради-структуры)
12. [catch, который никогда не сработает](#12-catch-который-никогда-не-сработает)
13. [Математика 1995 года на макросах](#13-математика-1995-года-на-макросах)
14. [goto](#14-goto)
15. [Баги калькулятора живут в трекере ОС](#15-баги-калькулятора-живут-в-трекере-ос)
16. [Забытые TODO из шаблонов Visual Studio](#16-забытые-todo-из-шаблонов-visual-studio)
17. [Магические числа в тестах](#17-магические-числа-в-тестах)
18. [Файлы-гиганты](#18-файлы-гиганты)

[Итог](#итог)

---

# Часть I. Архитектурные решения

## 1. Четыре языка в одном калькуляторе

**Решение.** Калькулятор написан на C# и трёх разновидностях C++:

| Проект | Язык | Что делает |
|---|---|---|
| `Calculator` | C# (UWP) | окна и разметка |
| `Calculator.ViewModels` | C# | логика экранов |
| `CalcManager` | C++ | движок вычислений |
| `CalcManager.Interop` | C++/WinRT | мост между C++ и C# |
| `GraphControl` | C++/CX | элемент графиков |
| `GraphingImpl` | C++ | движок графиков (в открытом коде — заглушки) |
| `TraceLogging` | C++ | телеметрия |
| `TraceLogging.Managed` | C# | тоже телеметрия |

И ещё четыре проекта тестов, всего 12 проектов.

**Контекст.** Это не наследие далёкого прошлого, а текущее состояние. До августа 2026 года ViewModels были написаны на
C++/CX: в коде было 62 `ref class`, сейчас осталось 18. Переезд на C# (#2491) затронул 263 файла, в нём
+22 362 и −26 045 строк. Это полсотни тысяч строк изменений, чтобы логика экранов калькулятора оказалась на языке, на
котором её стоило писать с самого начала. При этом график и движок остались на C++.

**Что не так.** Каждая граница между языками — это interop. Данные пересекают её как голые `int` и строки (см.
[9](#9-enum--int--enum) и [17](#17-магические-числа-в-тестах)), сборка требует тулчейнов C++ и C#, а чтобы понять одну
кнопку, приходится читать три языка.

**У нас.** Один язык, C#. Четыре проекта: ядро, приложение, приложение для Linux и тесты.

## 2. UWP: платформа, от которой ушли сами

**Решение.** Приложение — `AppContainerExe` на `Microsoft.NETCore.UniversalWindowsPlatform`.

**Контекст.** UWP была главной моделью приложений Windows 10. С тех пор Microsoft перевела разработчиков на Windows App
SDK и WinUI 3, а UWP осталась в режиме поддержки. Калькулятор, образцовое приложение из коробки Windows, так на ней и
остался.

**Что не так.** Калькулятор работает только на Windows, собирается только тулчейном UWP, и тесты тоже приходится
делать UWP-приложениями (см. [8](#8-четыре-проекта-тестов-и-юнит-тесты--это-приложения)).

**У нас.** .NET 10 и Avalonia: Windows, Linux, macOS и Android из одного кода.

## 3. C++/CX: диалект, заменённый в 2016 году

**Решение.** Элемент графиков написан на C++/CX: `ref class`, `^`-указатели, `Platform::String^`.

```cpp
// GraphControl/Control/Grapher.h
Windows::UI::Xaml::DispatcherTimer ^ m_TracingTrackingTimer;
Windows::UI::Core::CoreCursor ^ m_cachedCursor;
```

**Контекст.** C++/CX — расширение языка только для компилятора Microsoft. В 2016 году Microsoft представила
стандартный C++/WinRT и рекомендует его вместо C++/CX. В этом же репозитории мост `CalcManager.Interop` уже написан на
C++/WinRT. То есть в одном решении живут и старый, и новый способ, и они не согласованы.

**Что не так.** Нестандартный язык, который никто больше не учит, ни один другой компилятор не понимает, а сама
Microsoft рекомендует от него уходить.

**У нас.** Обычный C#.

## 4. Движок вычислений, который загружает курсы валют

**Решение.** Асинхронность построена на PPL (`concurrency::task`), библиотеке эпохи Windows 8. Смешнее всего, где она
живёт: в движке вычислений на C++.

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

**Контекст.** `CalcManager` — это ядро, которое должно умножать числа. Но в его интерфейсе есть
`TryLoadDataFromWebAsync`, и результат обновления курсов оно возвращает как `pair<bool, wstring>`: успех и строка с
временем. Ещё PPL используется для построения графика (`Grapher::TryPlotGraph`), для рендера
(`RenderMain::RunRenderPassAsync`) и в помощниках тестов.

**Что не так.** Математическое ядро знает про загрузку из сети. Результат описывается парой вместо типа. И всё это
завязано на асинхронную библиотеку, которую в современном C++ заменяют корутины, а в C# — `async`/`await`.

**У нас.** Загрузкой курсов занимается сервис приложения на `HttpClient`, ядро про сеть ничего не знает.
Асинхронность — `async`/`await`.

## 5. DirectX, чтобы нарисовать линию

**Решение.** `GraphControl/DirectX`: `DeviceResources`, `RenderMain`, `NearestPointRenderer`, `DirectXHelper`, всего
1 640 строк C++, чтобы вывести кривые на экран.

**Контекст.** Эти файлы выросли из шаблона Visual Studio «DirectX-приложение», и следы шаблона в них остались (см.
[16](#16-забытые-todo-из-шаблонов-visual-studio)). Кривые рисует закрытый движок через Direct2D.

**Что не так.** Графический API низкого уровня ради 2D-линий. Он привязывает калькулятор к Windows намертво: перенести
его куда-то можно только переписав.

**У нас.** Элемент, рисующий средствами Avalonia (под ней Skia), один и тот же код рисования на всех платформах. Кривые строятся в фоне с запасом за
краями экрана, так что перемещение плавное.

## 6. Открытый калькулятор с закрытым движком

**Решение.** В открытом репозитории вместо движка графиков лежат заглушки, `src/GraphingImpl/Mocks`.

```cpp
// GraphingImpl/Mocks/GraphRenderer.h
virtual HRESULT DrawD2D1(ID2D1Factory* /*pDirect2dFactory*/, ID2D1RenderTarget* /*pRenderTarget*/, bool& hasSomeMissingDataOut)
{
    hasSomeMissingDataOut = false;
    return S_OK;
}
```

Заглушка ничего не рисует и сообщает об успехе. Анализатор функций в ней `return nullptr;`, а любое введённое
выражение разбирается как «успешно».

**Контекст.** Соберите «открытый» калькулятор, и режим графиков покажет пустую сетку. Настоящий движок стоит только в
сборке Microsoft, и именно он:

- на `tan(10x)` думает пять секунд, нагружая процессор, и отвечает «This function is too complex to graph»;
- на `tan(100x)` рисует вертикальные линии через полюса и ставит точку трассировки на ось x с подписью y = 556,69;
- у автора ещё и ронял приложение при рендере.

**Что не так.** Самая сложная и самая сломанная часть закрыта, поэтому ни найти ошибку, ни исправить её сообщество не
может.

*Версия автора (шутка, а не утверждение): движок держат закрытым, потому что прятать дыры дешевле, чем их чинить.
Опровергнуть это просто: опубликовать код.*

**У нас.** Движок открыт: парсер, адаптивная выборка, интервальная арифметика, анализ функций. На него есть тесты.
`tan(10x)` строится за 11 мс.

## 7. Два проекта телеметрии

**Решение.** `TraceLogging` (C++) и `TraceLogging.Managed` (C#), а в `TraceLogger.cs` 20 видов событий:

```csharp
// Calculator.ViewModels/Common/TraceLogger.cs
private const string EventNameButtonUsage = "ButtonUsageInSession";
private const string EventNameInputPasted = "InputPasted";
private const string EventNameVariableChanged = "VariableChanged";
private const string EventNameGraphTheme = "GraphTheme";
private const string EventNameConverterInputReceived = "ConverterInputReceived";
...
```

**Контекст.** `ButtonUsageInSession` — это счётчик: сколько раз и в каком режиме вы нажали каждую кнопку. Ещё
отдельные события отправляются, когда вы вставляете текст, меняете переменную графика, тему графика или вводите
значение в конвертере.

**Что не так.** Калькулятору не нужно докладывать, как вы считаете. И на это у него два отдельных проекта на двух
языках.

**У нас.** Телеметрии нет. [PRIVACY.md](../PRIVACY.md) короче этого раздела.

## 8. Четыре проекта тестов, и юнит-тесты — это приложения

**Решение.** `Calculator.Tests` (C#), `CalculatorUnitTests` (C++), `CalculatorUITests` и `CalculatorUITestFramework`.
Оба проекта *юнит*-тестов — UWP-приложения:

```xml
<!-- Calculator.Tests/Calculator.Tests.csproj -->
<OutputType>AppContainerExe</OutputType>

<!-- CalculatorUnitTests/CalculatorUnitTests.vcxproj -->
<AppContainerApplication>true</AppContainerApplication>
<ApplicationType>Windows Store</ApplicationType>
```

**Контекст.** Тестам нужен поток интерфейса, поэтому у них свой помощник, который ждёт его с таймаутом `INFINITE`:

```cpp
// CalculatorUnitTests/AsyncHelper.h
static void RunOnUIThread(std::function<void()>&& action, DWORD timeout = INFINITE);
```

**Что не так.** Чтобы проверить, что 2 + 2 = 4, нужно собрать и развернуть приложение Windows Store. На Linux или в
обычном контейнере CI такие тесты не запустить. Движок графиков при этом не тестируется вообще: его нет в репозитории.

**У нас.** Ядро — обычная библиотека .NET, 249 тестов на xUnit, и ещё 36 тестов интерфейса работают без окна; `dotnet test` на любой ОС.

---

# Часть II. Код

## 9. enum → int → enum

**Решение.** У графиков есть нормальные перечисления ошибок:

```cpp
// GraphControl/Models/Equation.h
public enum class ErrorType { Evaluation, Syntax, Abort, };
public enum class EvaluationErrorCode { ... };
public enum class SyntaxErrorCode { ... };
```

А элемент графиков хранит ошибку в двух `int`:

```cpp
// GraphControl/Control/Grapher.h
int m_errorType;
int m_errorCode;
```

**Контекст.** Движок заполняет их через выходные параметры `int&`:

```cpp
// GraphControl/Control/Grapher.cpp
if ((graphExpression = m_solver->ParseInput(request, m_errorCode, m_errorType)))
...
m_solver->HRErrorToErrorInfo(m_renderMain->GetRenderError(), m_errorCode, m_errorType);
...
eq->GraphErrorType = static_cast<ErrorType>(m_errorType);
eq->GraphErrorCode = m_errorCode;
```

Тип ошибки по дороге снова становится перечислением, а код остаётся `int`. Дальше ViewModel по типу решает, к какому
перечислению привести код:

```csharp
// Calculator.ViewModels/GraphingCalculator/EquationViewModel.cs
public static string EquationErrorText(GraphControl.ErrorType errorType, int errorCode)
{
    if (errorType == GraphControl.ErrorType.Evaluation)
    {
        switch ((GraphControl.EvaluationErrorCode)errorCode)
```

И тест старательно превращает перечисление в `int`, чтобы метод превратил его обратно:

```csharp
// Calculator.Tests/GraphingViewModelTests.cs
EquationViewModel.EquationErrorText(
    GraphControl.ErrorType.Evaluation,
    (int)GraphControl.EvaluationErrorCode.Overflow));
```

**Что не так.** Компилятор не может проверить, что код ошибки соответствует её типу. Передайте код синтаксической
ошибки с типом `Evaluation`, и получите чужое сообщение без единого предупреждения.

**У нас.**

```csharp
public sealed class GraphInputException(GraphInputError error) : Exception($"Invalid graph input: {error}.")
{
    public GraphInputError Error { get; } = error;
}
```

Ошибка — перечисление от начала и до конца.

## 10. Шесть видов «слишком сложно»

**Решение.** В том же `switch`:

```csharp
case GraphControl.EvaluationErrorCode.TooComplexToSolve:
case GraphControl.EvaluationErrorCode.EquationTooComplexToSolve:
case GraphControl.EvaluationErrorCode.EquationTooComplexToSolveSymbolic:
case GraphControl.EvaluationErrorCode.EquationTooComplexToPlot:
case GraphControl.EvaluationErrorCode.InequalityTooComplexToSolve:
case GraphControl.EvaluationErrorCode.GE_TooComplexToSolve:
    return Resource("TooComplexToSolve");
```

| Код | Значение |
|---|---:|
| `TooComplexToSolve` | 4 |
| `EquationTooComplexToSolveSymbolic` | −7 |
| `EquationTooComplexToSolve` | −9 |
| `EquationTooComplexToPlot` | −10 |
| `InequalityTooComplexToSolve` | −41 |
| `GE_TooComplexToSolve` | −506 |

**Контекст.** Значения идут вразброс: положительные, отрицательные, с дырами до −506. Похоже на коды, накопленные за
годы в разных частях закрытого движка.

**Что не так.** Шесть способов сдаться и одно сообщение для пользователя. Судя по `tan(10x)`, это самый нагруженный
путь в коде.

## 11. Структура ради структуры

**Решение.**

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

**Контекст.** `HISTORYITEM` — обёртка из одного поля. Токен выражения — `pair<wstring, int>`, строка и `int`, смысл
которого из типа не узнать. Результат вычисления хранится строкой. Имена капсом и префиксы `sp` — венгерская нотация
из Win32 90-х.

**Что не так.** Две структуры там, где хватило бы одной, анонимные пары вместо типов и лишние `shared_ptr` на
коллекции.

**У нас.**

```csharp
public sealed record HistoryEntry(IReadOnlyList<ExpressionToken> Expression, BigDecimal Result);
```

## 12. catch, который никогда не сработает

**Решение.** `std::vector`, обёрнутый в коды ошибок в стиле COM:

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

**Контекст.** Рядом `GetAt` сделан правильно, через `m_vector.at(index)`, который как раз бросает `out_of_range`.
Похоже, `SetAt` скопировали с него и заменили `.at()` на `[]`. А `GetSize` возвращает `ResultCode`, который всегда
`S_OK`:

```cpp
ResultCode GetSize(_Out_ unsigned int* size)
{
    *size = static_cast<unsigned>(m_vector.size());
    return S_OK;
}
```

**Что не так.** `operator[]` не проверяет границы и ничего не бросает. Выход за границы здесь — неопределённое
поведение, а не `E_BOUNDS`. Обёртка над стандартным контейнером превращает исключения в HRESULT, а размер — в
выходной параметр.

## 13. Математика 1995 года на макросах

**Решение.** Сердце калькулятора — библиотека рациональных чисел `ratpak`:

```cpp
// CalcManager/Ratpack/ratpak.h
//  Package Title  ratpak
//  File           ratpak.h
//  Copyright      (C) 1995-99 Microsoft
//  Date           01-16-95
```

Память управляется вручную: 233 вызова `destroyrat`/`destroynum` на весь движок. Копирование числа — макрос:

```cpp
#define DUPRAT(a, b)            \
    destroyrat(a);              \
    createrat(a);               \
    DUPNUM((a)->pp, (b)->pp);   \
    DUPNUM((a)->pq, (b)->pq);
```

**Контекст.** Макрос из четырёх инструкций без `do { } while (0)`. Напишите `if (x) DUPRAT(a, b);`, и под `if`
попадёт только первая строка: число уничтожится по условию, а остальные три выполнятся всегда. Это классика, которую
разбирают на первом курсе. Отладочная версия `createrat` пишет в `OutputDebugString` каждое создание числа с файлом и
строкой: так в 1995 году искали утечки памяти.

Как это используется в арксинусе:

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

Переменная называется `phack`, а комментарий предлагает обойти «по-настоящему плохую часть кривой».

**Что не так.** В 2026 году ядро калькулятора держится на коде C 30-летней давности с ручным освобождением памяти и
макросами, опасными при обычном `if`.

**У нас.** `BigDecimal` и `BigMath` на C#: неизменяемые числа, сборщик мусора, ни одного макроса.

## 14. goto

**Решение.**

```cpp
// CalcManager/CEngine/scicomm.cpp
        DoPrecedenceCheckAgain:
            ...
                    // Precedence Inversion Higher to lower can happen which needs explicit enclosure of brackets
                    ...
                    m_HistoryCollector.PopLastOpndStart();
                    goto DoPrecedenceCheckAgain;
```

**Контекст.** Метка стоит на строке 276, прыжок — на строке 335. Так в калькуляторе 2026 года разбирается приоритет
операций. Рядом комментарий, где разработчики сами описывают «инверсию приоритета», которую приходится заклеивать
лишними скобками в журнале.

**Что не так.** Цикл, замаскированный под `goto`, внутри функции `CCalcEngine::ProcessCommandWorker` длиной 770 строк (111–881). Дейкстра написал «Go To Statement
Considered Harmful» в 1968 году.

## 15. Баги калькулятора живут в трекере ОС

**Решение.**

```csharp
// Calculator/Views/MainPage.xaml.cs
if (deferral == null)
{
    // FIXME: https://microsoft.visualstudio.com/DefaultCollection/OS/_workitems/edit/47775705/
    TraceLogger.GetInstance().LogRecallError("55e29ba5-6097-40ec-8960-458750be3039");
    return;
}
```

**Контекст.** В открытом коде ссылка на **закрытый** внутренний трекер, причём в проект **OS**. Вместо исправления
в телеметрию отправляется захардкоженный GUID. Баги калькулятора и операционной системы лежат в одной куче.

И ещё:

```csharp
// Calculator/Views/DateCalculator.xaml.cs
// We choose 2550 as the max year because CalendarDatePicker experiences clipping
// issues just after 2558.  We would like 9999 but will need to wait for a platform
// fix before we use a higher max year.  This platform issue is tracked by
// TODO: MSFT-9273247
private const int c_maxYear = 2550;
```

Калькулятор дат не умеет годы после 2550, потому что **собственный** элемент выбора даты Microsoft обрезается после
2558. Калькулятор ждёт исправления платформы. Платформа — это тоже Microsoft.

```csharp
// Calculator/App.xaml.cs
// TODO: MSFT 14645325: Set this directly from XAML.
// Currently this is bugged so the property is only respected from code-behind.
HighContrastAdjustment = ApplicationHighContrastAdjustment.None;
```

Баг собственного XAML, обойдённый в коде.

**Что не так.** Если так выглядит код, который Microsoft не стесняется показать, и если его баги лежат в трекере ОС,
остаётся только гадать, как выглядит код, который она не показывает. Мы не знаем. Мы просто спрашиваем.

## 16. Забытые TODO из шаблонов Visual Studio

**Решение.**

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

**Контекст.** Это не заметки разработчиков, а текст из шаблонов проектов Visual Studio: «DirectX-приложение» и «Пустое
приложение UWP». Шаблон оставляет такие TODO, чтобы разработчик их заменил.

**Что не так.** Рендерер графиков калькулятора Windows начинался как «File → New Project», и заглушку так никто и не
заменил. Пустой `if` с TODO в тестовом приложении живёт там с января 2019 года, с первых дней открытого репозитория.

## 17. Магические числа в тестах

**Решение.**

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

**Контекст.** Команды калькулятора пересекают границу C++ и C# как голые `int` (см. [1](#1-четыре-языка-в-одном-калькуляторе)).
Общего определения нет, поэтому тест объявляет номера заново, руками.

**Что не так.** Поменяется номер в C++, и тест тихо начнёт проверять что-то другое. Защищать от этого должны как раз
тесты.

## 18. Файлы-гиганты

```
Оригинал                                         строк
Calculator/App.xaml                     ████████████████████████▌ 2444
StandardCalculatorViewModel.cs          ████████████████████▋     2060
CalcManager/Ratpack/conv.cpp            ███████████████▍          1533
UnitConverterViewModel.cs               ███████████████▎          1525
GraphingNumPad.xaml                     ██████████████▊           1474
Calculator.xaml                         ██████████████            1406

BTL Calculator
BigMath.cs (вся точная математика)      █████████▊                 978
FunctionAnalyzer.cs (весь анализ)       █████████▋                 967
CurveBuilder.cs (построение кривых)     ███████▌                   755
```

**Контекст.** `App.xaml` — это ресурсы и стили приложения, 2 444 строки. `StandardCalculatorViewModel.cs` — одна
ViewModel на 2 060 строк.

**Что не так.** Одни только стили оригинала длиннее, чем вся наша точная арифметика с тригонометрией, логарифмами,
гиперболическими функциями и гамма-функцией.

---

## Итог

| | Оригинал | BTL Calculator |
|---|---|---|
| Языки | C#, C++, C++/CX, C++/WinRT | C# |
| Проекты | 12 | 4 |
| Платформы | Windows | Windows, Linux, Android, iOS, macOS |
| Движок графиков | закрыт, в открытом коде заглушки | открыт, с тестами |
| `tan(10x)` | «Too complex to graph» через 5 с | 11 мс |
| Тесты | 4 проекта, юнит-тесты — UWP-приложения | 249 тестов ядра и 36 интерфейса, `dotnet test` на любой ОС |
| Телеметрия | 2 проекта, 20 видов событий | нет |
| Размер | ≈ 77 000 строк (26 тыс. C++, 36 тыс. C#, 15 тыс. XAML) без движка | 18 723 строки с движком и тестами |

Это калькулятор. Он принимает числа и отдаёт числа. Мы переписали его, чтобы доказать, что это можно сделать просто,
и чтобы вы посмеялись. Надеемся, получилось и то и другое.

*Проект не связан с Microsoft, не одобрен и не спонсирован ею. «Windows» и «Microsoft» — товарные знаки Microsoft
Corporation. Фрагменты кода цитируются из [microsoft/calculator](https://github.com/microsoft/calculator) под
лицензией MIT.*
