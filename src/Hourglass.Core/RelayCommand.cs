using System.Windows.Input;

namespace Hourglass.Core;

/// <summary>Minimal <see cref="ICommand"/>; the view model calls <see cref="RaiseCanExecuteChanged"/> after state changes.</summary>
public sealed class RelayCommand(Action execute, Func<bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute();

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            execute();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
