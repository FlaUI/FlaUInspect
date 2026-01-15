namespace FlaUInspect.Core;

public enum FindByType {
    ByAutomationId,
    ByName,
    ByClassName,
    ByControlType,
    FindFirstByXPath,
    ByText,
    ByFrameworkId,
    ByLocalizedControlType,
    ByValue,
}

public static class FindByTypeValues {
    public static FindByType[] All { get; } = Enum.GetValues<FindByType>();
}
