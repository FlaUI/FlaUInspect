using System.Diagnostics;
using System.Windows.Input;

namespace FlaUInspect.Core;

/// <summary>
/// Class to easily create ICommands
/// Last updated: 13.01.2015
/// </summary>
public class RelayCommand(Action<object?> methodToExecute, Func<object, bool>? canExecuteEvaluator = null) : ICommand {

    [DebuggerStepThrough]
    public bool CanExecute(object? parameter) {
        if (parameter == null) {
            return true;
        }
        return canExecuteEvaluator == null || canExecuteEvaluator.Invoke(parameter);
    }

    public event EventHandler? CanExecuteChanged {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public void Execute(object? parameter) {
        methodToExecute.Invoke(parameter);
    }
}