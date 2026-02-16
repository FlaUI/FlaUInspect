using FlaUInspect.Core;

namespace FlaUInspect.ViewModels;

public class PatternActionItem(string Title, bool IsEnable, Action Action) {
    public string Title { get; } = Title;
    public bool IsEnable { get; } = IsEnable;
    public Action Action { get; } = Action;
}
