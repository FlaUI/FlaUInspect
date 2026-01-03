namespace FlaUInspect.Core;

public interface IDialogViewModel {
    bool CanClose { get; }
    void Save();
    void Close();
}