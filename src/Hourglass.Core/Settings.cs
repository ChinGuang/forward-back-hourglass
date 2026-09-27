using System.Text.Json;

namespace Hourglass.Core;

public sealed record HourglassSettings(double ForwardSpeed = SpeedPresets.Default, double BackwardSpeed = SpeedPresets.Default);

public interface ISettingsStore
{
    HourglassSettings Load();

    void Save(HourglassSettings settings);
}

/// <summary>
/// Stores settings as JSON. A missing or unreadable file falls back to defaults instead of failing startup.
/// Values are returned as stored; the view model snaps them to valid presets.
/// </summary>
public sealed class JsonSettingsStore(string filePath) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary><c>%APPDATA%\ForwardBackHourglass\settings.json</c> on Windows.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ForwardBackHourglass",
        "settings.json");

    public string FilePath { get; } = filePath;

    public HourglassSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new HourglassSettings();
            }

            return JsonSerializer.Deserialize<HourglassSettings>(File.ReadAllText(FilePath), Options) ?? new HourglassSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new HourglassSettings();
        }
    }

    public void Save(HourglassSettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Remembering speeds is a convenience; never crash the timer over it.
        }
    }
}
