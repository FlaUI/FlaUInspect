namespace FlaUInspect.Core.Extensions;

public static class TaskExtensions {
	public static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan period, T defaultValue) {
		try {
			return (Task?)await Task.WhenAny(task, Task.Delay(period)) == task ? await task : defaultValue;
		}
		catch (Exception) {
			return defaultValue;
		}
	}
}