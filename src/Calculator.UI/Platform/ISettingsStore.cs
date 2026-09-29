namespace Calculator.UI.Platform;

/// <summary>Settings kept between runs: theme, language, last mode, window size, graph options.</summary>
public interface ISettingsStore
{
    string GetString(string key, string defaultValue);

    int GetInt(string key, int defaultValue);

    double GetDouble(string key, double defaultValue);

    void Set(string key, string value);

    void Set(string key, int value);

    void Set(string key, double value);
}
