using System.Windows.Input;

namespace FlaUInspect.Core;

public class AsyncRelayCommand : ObservableObject, ICommand {
    private readonly Func<bool> _canExecute;
    private readonly Func<Task> _execute;
    private bool _isRunning = false;

    /// <summary>Initializes a new instance of the <see cref="AsyncRelayCommand"/> class. </summary>
    /// <param name="execute">The function to execute. </param>
    public AsyncRelayCommand(Func<Task> execute)
        : this(execute, null) {
    }

    /// <summary>Initializes a new instance of the <see cref="AsyncRelayCommand"/> class. </summary>
    /// <param name="execute">The function. </param>
    /// <param name="canExecute">The predicate to check whether the function can be executed. </param>
    public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute) {
        if (execute == null)
            throw new ArgumentNullException("execute");

        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>Gets a value indicating whether the command is currently running. </summary>
    public bool IsRunning {
        get => _isRunning;
        private set {
            _isRunning = value;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>Gets a value indicating whether the command can execute in its current state. </summary>
    public bool CanExecute => !IsRunning && (_canExecute == null || _canExecute());

    /// <summary>Occurs when changes occur that affect whether or not the command should execute. </summary>
    public event EventHandler CanExecuteChanged;

    void ICommand.Execute(object parameter) {
        Execute();
    }

    bool ICommand.CanExecute(object parameter) {
        return CanExecute;
    }

    /// <summary>Defines the method to be called when the command is invoked. </summary>
    protected async void Execute() {
        Task? task = _execute();

        if (task != null) {
            IsRunning = true;
            await task;
            IsRunning = false;
        }
    }

    /// <summary>Gets a value indicating whether the command can execute in its current state. </summary>
    /// <summary>Tries to execute the command by checking the <see cref="CanExecute"/> property 
    /// and executes the command only when it can be executed. </summary>
    /// <returns>True if command has been executed; false otherwise. </returns>
    public bool TryExecute() {
        if (!CanExecute)
            return false;
        Execute();
        return true;
    }

    /// <summary>Triggers the CanExecuteChanged event and a property changed event on the CanExecute property. </summary>
    public void RaiseCanExecuteChanged() {
        RaisePropertyChanged(nameof(CanExecute));

        EventHandler? copy = CanExecuteChanged;
        if (copy != null)
            copy(this, new EventArgs());
    }
}