using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calculator.UI.Platform;

/// <summary>
/// Settings in a JSON file in the app's data folder (<see cref="AppFolders.Data"/>/settings.json). Values are stored as
/// invariant strings; reading uses a source-generated serializer, so it works in trimmed and Native AOT builds.
/// Every change is written at once, through a temporary file, so a crash never leaves half a file.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, string> _values;

    public JsonSettingsStore()
        : this(Path.Combine(AppFolders.Data, "settings.json"))
    {
    }

    public JsonSettingsStore(string path)
    {
        _path = path;
        _values = Load(path);
    }

    public string GetString(string key, string defaultValue) => TryGet(key, out string text) ? text : defaultValue;

    public int GetInt(string key, int defaultValue) =>
        TryGet(key, out string text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : defaultValue;

    public double GetDouble(string key, double defaultValue) =>
        TryGet(key, out string text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : defaultValue;

    public void Set(string key, string value) => Store(key, value);

    public void Set(string key, int value) => Store(key, value.ToString(CultureInfo.InvariantCulture));

    public void Set(string key, double value) => Store(key, value.ToString("R", CultureInfo.InvariantCulture));

    private bool TryGet(string key, out string text)
    {
        lock (_lock)
        {
            return _values.TryGetValue(key, out text!);
        }
    }

    private void Store(string key, string text)
    {
        lock (_lock)
        {
            if (_values.TryGetValue(key, out string? current) && current == text)
            {
                return;
            }

            _values[key] = text;
            Save();
        }
    }

    private static Dictionary<string, string> Load(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.DictionaryStringString) ?? [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // No file yet, or an unreadable one: start with the defaults; the next change writes a new file.
            return [];
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string temporary = _path + ".tmp";
            using (FileStream stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, _values, SettingsJsonContext.Default.DictionaryStringString);
            }

            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A read-only data folder must not break the calculator; the values stay for this session.
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
