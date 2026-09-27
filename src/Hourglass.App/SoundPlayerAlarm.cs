using System.ComponentModel;
using System.IO;
using System.Media;
using System.Windows;
using Hourglass.Core;

namespace Hourglass.App;

/// <summary>Loops the bundled ring.wav. Works offline: the sound is compiled into the executable.</summary>
public sealed class SoundPlayerAlarm : IAlarmPlayer
{
    private static readonly Uri RingUri = new("pack://application:,,,/Assets/ring.wav");

    private SoundPlayer? _player;

    public void Play()
    {
        try
        {
            _player ??= CreatePlayer();
            _player.PlayLooping();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or Win32Exception)
        {
            // No usable audio device: fall back to the system alert sound so the user still hears something.
            SystemSounds.Exclamation.Play();
        }
    }

    public void Stop() => _player?.Stop();

    private static SoundPlayer CreatePlayer()
    {
        Stream stream = Application.GetResourceStream(RingUri)?.Stream
            ?? throw new InvalidOperationException("Ring sound resource is missing.");
        var player = new SoundPlayer(stream);
        player.Load();
        return player;
    }
}
