using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Logging;
using Avalonia.Threading;
using Calculator.UI;
using Calculator.UI.Platform;
using Calculator.UI.Services;
using Calculator.UI.Views;

[assembly: AvaloniaTestApplication(typeof(Calculator.UI.Tests.TestApp))]

namespace Calculator.UI.Tests;

/// <summary>The app (its resources and styles) on the headless platform, and helpers to drive views in tests.</summary>
public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp()
    {
        Logger.Sink = BindingErrors.Sink;
        return AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    /// <summary>
    /// Services with settings and the rate cache in temporary files, so tests never touch the user's files, and rates
    /// that come from a canned answer, so tests never use the network.
    /// </summary>
    public static AppServices CreateServices()
    {
        string folder = Path.Combine(Path.GetTempPath(), "BtlCalculatorTests", Guid.NewGuid().ToString("N"));
        var rates = new ExchangeRateService(new HttpClient(new CannedRates()), Path.Combine(folder, "exchange-rates.json"));
        return new AppServices(new JsonSettingsStore(Path.Combine(folder, "settings.json")), rates);
    }

    /// <summary>Answers every request with rates in the format of ExchangeRate-API.</summary>
    private sealed class CannedRates : HttpMessageHandler
    {
        private const string Json = """{"result":"success","time_last_update_unix":1759000000,"base_code":"USD","rates":{"USD":1,"EUR":0.85,"RUB":82.5,"BYN":3.0238,"JPY":148.2}}""";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(Json) });
    }

    /// <summary>The main view in a window of the default size of the app.</summary>
    public static (Window Window, MainView View) ShowMainView(double width = 322, double height = 540)
    {
        var view = new MainView(CreateServices());
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    /// <summary>
    /// Lets the UI run for the given time: layout, bindings, timers and animations. It waits asynchronously, so the
    /// dispatcher keeps running its timers (the render timer of the headless platform is one), and forces a frame now
    /// and then.
    /// </summary>
    public static async Task RunAsync(TimeSpan duration)
    {
        DateTime end = DateTime.UtcNow + duration;
        do
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(16);
        }
        while (DateTime.UtcNow < end);
        Dispatcher.UIThread.RunJobs();
    }
}
/// <summary>Collects the binding errors Avalonia logs (it does not throw them), so tests can fail on them.</summary>
public static class BindingErrors
{
    private static readonly List<string> Errors = [];

    public static ILogSink Sink { get; } = new CollectingSink();

    public static IReadOnlyList<string> TakeAll()
    {
        lock (Errors)
        {
            string[] errors = [.. Errors];
            Errors.Clear();
            return errors;
        }
    }

    private sealed class CollectingSink : ILogSink
    {
        public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning && area == LogArea.Binding;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
            Log(level, area, source, messageTemplate, []);

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            if (IsEnabled(level, area))
            {
                lock (Errors)
                {
                    Errors.Add($"{source?.GetType().Name}: {Format(messageTemplate, propertyValues)}");
                }
            }
        }

        private static string Format(string template, object?[] values)
        {
            int index = 0;
            return System.Text.RegularExpressions.Regex.Replace(template, @"\{[^}]+\}", _ => index < values.Length ? values[index++]?.ToString() ?? "null" : "?");
        }
    }
}
