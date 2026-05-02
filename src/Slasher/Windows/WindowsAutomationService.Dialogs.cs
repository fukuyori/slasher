using Slasher.Api;

namespace Slasher.Windows;

public sealed partial class WindowsAutomationService
{
    public bool ShowMessageBox(MessageBoxRequest request, out MessageBoxResponse? response, out ErrorResponse? error)
    {
        response = null;
        error = null;

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            error = new ErrorResponse("invalid_message_box_text", "Message text is required.");
            return false;
        }

        var title = string.IsNullOrWhiteSpace(request.Title) ? "Slasher" : request.Title.Trim();
        var result = NativeMethods.MessageBox(
            IntPtr.Zero,
            request.Text,
            title,
            NativeMethods.MbOk | NativeMethods.MbIconInformation | NativeMethods.MbTopmost);
        if (result == 0)
        {
            error = new ErrorResponse("message_box_failed", "MessageBox failed.");
            return false;
        }

        response = new MessageBoxResponse(title, request.Text, result);
        return true;
    }
}
