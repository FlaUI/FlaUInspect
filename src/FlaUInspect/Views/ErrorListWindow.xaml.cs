using FlaUInspect.Core.Logger;

namespace FlaUInspect.Views;

public partial class ErrorListWindow {
    public ErrorListWindow(InternalLogger logger) {
        InitializeComponent();

        List<InternalLoggerMessage> internalLoggerMessages = [.. logger.Messages];
        ErrorItemsControl.ItemsSource = internalLoggerMessages;
    }
}