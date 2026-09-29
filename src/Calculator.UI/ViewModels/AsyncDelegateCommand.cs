using System.Windows.Input;

namespace Calculator.UI.ViewModels;

/// <summary>
/// A command running an asynchronous operation. ICommand.Execute cannot return a task, so this (with its generic
/// twin below) is the single place where a task is started without being awaited by the caller; the operation must
/// handle its expected failures itself. The command is disabled while the operation runs.
/// </summary>
public sealed class AsyncDelegateCommand(Func<Task> execute) : ICommand
{
    private bool _isRunning;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isRunning;

    public async void Execute(object? parameter)
    {
        if (_isRunning)
        {
            return;
        }

        SetRunning(true);
        try
        {
            await execute();
        }
        finally
        {
            SetRunning(false);
        }
    }

    private void SetRunning(bool value)
    {
        _isRunning = value;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>An <see cref="AsyncDelegateCommand"/> with a typed parameter (the item a list button acts on).</summary>
public sealed class AsyncDelegateCommand<T>(Func<T, Task> execute) : ICommand
{
    private bool _isRunning;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isRunning && parameter is T;

    public async void Execute(object? parameter)
    {
        if (_isRunning || parameter is not T value)
        {
            return;
        }

        SetRunning(true);
        try
        {
            await execute(value);
        }
        finally
        {
            SetRunning(false);
        }
    }

    private void SetRunning(bool value)
    {
        _isRunning = value;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
