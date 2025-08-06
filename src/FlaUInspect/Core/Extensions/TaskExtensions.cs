namespace FlaUInspect.Core.Extensions;

public static class TaskExtensions {
    public static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan period, T defaultValue) {
        try {
            Task timeoutTask = Task.Delay(period);
            Task completedTask = await Task.WhenAny(task, timeoutTask);

            if (completedTask == task) {
                T result = await task;
                return result;
            }
            return defaultValue;
        }
        catch (Exception) {
            return defaultValue;
        }
    }
}