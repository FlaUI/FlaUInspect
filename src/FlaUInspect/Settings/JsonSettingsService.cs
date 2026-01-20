using System.IO;
using System.Text.Json;

namespace FlaUInspect.Settings;

public sealed class JsonSettingsService<T> : ISettingsService<T> where T : class, new() {
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options;

    public JsonSettingsService(string filePath) {
        _filePath = filePath;

        _options = new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public T Load() {
        if (!File.Exists(_filePath))
            return new T();

        string json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<T>(json, _options) ?? new T();
    }

    public void Save(T settings) {
        string json = JsonSerializer.Serialize(settings, _options);
        File.WriteAllText(_filePath, json);
    }
}