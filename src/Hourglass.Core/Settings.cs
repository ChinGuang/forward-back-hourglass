using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hourglass.Core;

/// <param name="Timer">The timer as it was when the app closed; null (e.g. in files from older versions) means a fresh timer.</param>
public sealed record HourglassSettings(
    double ForwardSpeed = SpeedPresets.Default,
    double BackwardSpeed = SpeedPresets.Default,
    TimerSnapshot? Timer = null);

public interface ISettingsStore
{
    HourglassSettings Load();

    void Save(HourglassSettings settings);
}

/// <summary>
/// Stores settings as JSON. A missing or unreadable file falls back to defaults instead of failing startup.
/// Values are returned as stored; the view model snaps speeds to presets and validates the timer.
/// </summary>
public sealed class JsonSettingsStore(string filePath) : ISettingsStore
{
    // States are written by name ("Backward"), so the file stays readable and doesn't depend on enum order.
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

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

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(FilePath));
            return Parse(document.RootElement);
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
            // Remembering speeds and the timer is a convenience; never crash the app over it.
        }
    }

    /// <summary>Reads each part on its own, so a damaged timer section can't cost the user their speeds.</summary>
    private static HourglassSettings Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new HourglassSettings();
        }

        TimerSnapshot? timer = null;
        if (root.TryGetProperty(nameof(HourglassSettings.Timer), out JsonElement timerJson) && timerJson.ValueKind == JsonValueKind.Object)
        {
            try
            {
                timer = timerJson.Deserialize<TimerSnapshot>(Options);
            }
            catch (JsonException)
            {
                // Unreadable timer: start fresh, keep the speeds.
            }
        }

        return new HourglassSettings(
            ReadSpeed(root, nameof(HourglassSettings.ForwardSpeed)),
            ReadSpeed(root, nameof(HourglassSettings.BackwardSpeed)),
            timer);
    }

    private static double ReadSpeed(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetDouble(out double speed)
            ? speed
            : SpeedPresets.Default;
}
