using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private NumadoraLocalHostCallResult? ExecuteNumadoraDialogHostCall(
        NumadoraHostCall hostCall,
        NumadoraPolicyDecision policyDecision)
    {
        if (!hostCall.Module.Equals("slasher_dialog", StringComparison.OrdinalIgnoreCase)
            || (!hostCall.Function.Equals("Message", StringComparison.OrdinalIgnoreCase)
                && !hostCall.Function.Equals("Alert", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var dialog = ParseNumadoraDialogArgs(hostCall);
        if (string.IsNullOrWhiteSpace(dialog.Text))
        {
            return NumadoraLocalHostCallResult.Failed(
                "numadora_host_call_invalid_arguments",
                $"{hostCall.Module}.{hostCall.Function} requires message text.",
                executedBy: "slasher-dialog");
        }

        if (!_automation.ShowMessageBox(
            new MessageBoxRequest(dialog.Text, dialog.Title),
            out var response,
            out var error)
            || response is null)
        {
            return NumadoraLocalHostCallResult.Failed(
                error?.Code ?? "message_box_failed",
                error?.Message ?? "Failed to show message box.",
                executedBy: "slasher-dialog",
                expected: new { shown = true, dialog.Title, dialog.Text },
                actual: new { shown = false });
        }

        return NumadoraLocalHostCallResult.Passed(
            new
            {
                shown = true,
                response.Title,
                response.Text,
                response.Button,
                executedBy = "slasher-dialog",
                policyAllowed = true,
                policyCode = policyDecision.Code
            },
            executedBy: "slasher-dialog");
    }

    private static NumadoraDialogArgs ParseNumadoraDialogArgs(NumadoraHostCall hostCall)
    {
        var raw = string.Join(' ', hostCall.Arguments).Trim();
        if (hostCall.Function.Equals("Alert", StringComparison.OrdinalIgnoreCase))
        {
            return new NumadoraDialogArgs(DecodeNumadoraDialogText(raw), "Slasher");
        }

        var parts = raw.Split('\t', 2);
        return parts.Length == 2
            ? new NumadoraDialogArgs(DecodeNumadoraDialogText(parts[1].Trim()), DecodeNumadoraDialogText(parts[0].Trim()))
            : new NumadoraDialogArgs(DecodeNumadoraDialogText(raw), "Slasher");
    }

    private static string DecodeNumadoraDialogText(string value)
    {
        return value
            .Replace("\\r\\n", "\r\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);
    }

    private readonly record struct NumadoraDialogArgs(string Text, string Title);
}
