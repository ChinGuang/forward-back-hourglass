namespace Hourglass.Core;

/// <summary>The window in front right now.</summary>
/// <param name="ProcessFileName">Its program file name, e.g. <c>Code.exe</c>.</param>
/// <param name="WindowHandle">Identifies the window, so each browser window keeps its own last known URL.</param>
/// <param name="AddressBarText">For supported browsers, what the address bar shows; null if it couldn't be read.</param>
public sealed record ActiveWindow(string ProcessFileName, long WindowHandle, string? AddressBarText = null);

/// <summary>Reports which window is in front whenever that (or a browser's address bar) changes.</summary>
public interface IForegroundWatcher
{
    event EventHandler<ActiveWindow?>? Changed;

    void Start();

    void Stop();
}
