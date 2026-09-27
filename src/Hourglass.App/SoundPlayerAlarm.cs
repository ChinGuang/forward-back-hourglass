using System.ComponentModel;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using Hourglass.Core;

namespace Hourglass.App;

/// <summary>Loops the bundled ring.wav. Works offline: the sound is compiled into the executable.</summary>
public sealed class SoundPlayerAlarm : IAlarmPlayer
{
    private static readonly Uri RingUri = new("pack://application:,,,/Assets/ring.wav");

    private readonly DispatcherTimer _fallbackBeep = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private SoundPlayer? _player;

    public SoundPlayerAlarm()
    {
        _fallbackBeep.Tick += (_, _) => SystemSounds.Exclamation.Play();
    }

    public void Play()
    {
        try
        {
            _player ??= CreatePlayer();
            _player.PlayLooping();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or TimeoutException or Win32Exception)
        {
            // The WAV couldn't be played: keep repeating the system alert sound instead, until Stop.
            SystemSounds.Exclamation.Play();
            _fallbackBeep.Start();
        }
    }

    public void Stop()
    {
        _fallbackBeep.Stop();
        _player?.Stop();
    }

    private static SoundPlayer CreatePlayer()
    {
        Stream stream = Application.GetResourceStream(RingUri)?.Stream
            ?? throw new InvalidOperationException("Ring sound resource is missing.");
        var player = new SoundPlayer(stream);
        player.Load();
        return player;
    }
}
