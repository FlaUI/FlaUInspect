using System.Windows.Input;

namespace FlaUInspect.Core;

public class AsyncRelayCommand : ObservableObject, ICommand {
	private readonly Func<object?, bool>? _canExecute;
	private readonly Func<object?, Task> _execute;

	/// <summary>Initializes a new instance of the <see cref="AsyncRelayCommand"/> class. </summary>
	/// <param name="execute">The function to execute. </param>
	public AsyncRelayCommand(Func<object?, Task> execute) : this(execute, null) {
	}

	/// <summary>Initializes a new instance of the <see cref="AsyncRelayCommand"/> class. </summary>
	/// <param name="execute">The function. </param>
	/// <param name="canExecute">The predicate to check whether the function can be executed. </param>
	public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute) {
		ArgumentNullException.ThrowIfNull(execute);

		_execute = execute;
		_canExecute = canExecute;
	}

	/// <summary>Gets a value indicating whether the command is currently running. </summary>
	public bool IsRunning {
		get;
		private set {
			field = value;
			RaiseCanExecuteChanged();
		}
	}

	/// <summary>Gets a value indicating whether the command can execute in its current state. </summary>
	public bool CanExecute(object? parameter) => !IsRunning && (_canExecute?.Invoke(parameter) != false);

	/// <summary>Occurs when changes occur that affect whether or not the command should execute. </summary>
	public event EventHandler? CanExecuteChanged;

	/// <summary>Defines the method to be called when the command is invoked. </summary>
	async void ICommand.Execute(object? parameter) {
		var task = _execute(parameter);
		if (task == null)
			return;

		IsRunning = true;
		await task;
		IsRunning = false;
	}

	/// <summary>Triggers the CanExecuteChanged event and a property changed event on the CanExecute property. </summary>
	public void RaiseCanExecuteChanged() {
		RaisePropertyChanged(nameof(CanExecute));
		CanExecuteChanged?.Invoke(this, new EventArgs());
	}
}