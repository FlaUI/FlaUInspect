namespace FlaUInspect.Core;

public interface IDialogViewModel {
    string Title { get; }
    string CloseButtonText { get; }
    string SaveButtonText { get; }
    bool IsSaveVisible  { get; }
    bool IsCloseVisible { get; }
    bool CanClose { get; }
    void Save();
    void Close();
}