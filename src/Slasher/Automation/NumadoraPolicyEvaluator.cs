namespace Slasher.Automation;

public sealed class NumadoraPolicyEvaluator
{
    public NumadoraPolicyDecision Evaluate(NumadoraPolicyInput input)
    {
        if (input.Capability is null)
        {
            return Deny("numadora_policy_missing_capability", "Host call has no registered capability metadata.");
        }

        if (string.IsNullOrWhiteSpace(input.Purpose))
        {
            return Deny("numadora_policy_missing_purpose", "Host call is missing a run purpose.");
        }

        if (IsDangerousClass(input.Capability.CapabilityClass) || IsDangerousProfile(input.Capability.Profile))
        {
            return Deny("numadora_policy_capability_blocked", $"Capability '{input.Capability.CapabilityClass}' requires an explicit policy.");
        }

        if (HasSensitiveLineage(input))
        {
            return Deny("numadora_policy_sensitive_lineage", "Sensitive lineage is not allowed for this host call.");
        }

        if (RequiresTargetIdentity(input.Capability) && input.Target is null)
        {
            return Deny("numadora_policy_missing_target", "Host call requires selected or foreground target identity.");
        }

        if (input.Capability.Module.Equals("slasher_io", StringComparison.OrdinalIgnoreCase))
        {
            return Allow("numadora_policy_allowed_local_observe", "slasher_io host call is allowed in the local observation profile.");
        }

        if (input.Capability.Profile.Equals("observe", StringComparison.OrdinalIgnoreCase))
        {
            return Allow("numadora_policy_allowed_observe", "Observe profile host call is allowed.");
        }

        if (input.Capability.Module.Equals("slasher_app", StringComparison.OrdinalIgnoreCase)
            && input.Capability.Function.Equals("Start", StringComparison.OrdinalIgnoreCase)
            && input.Capability.CapabilityClass.Equals("Process/app", StringComparison.OrdinalIgnoreCase))
        {
            return Allow("numadora_policy_allowed_process_app_start", "slasher_app.Start is allowed with local process metadata auditing.");
        }

        if (input.Capability.Module.Equals("slasher_window", StringComparison.OrdinalIgnoreCase)
            && input.Capability.Function.Equals("Focus", StringComparison.OrdinalIgnoreCase)
            && input.Target is not null)
        {
            return Allow("numadora_policy_allowed_window_focus", "slasher_window.Focus is allowed with explicit target identity.");
        }

        if (input.Capability.Module.Equals("slasher_input", StringComparison.OrdinalIgnoreCase)
            && IsApprovedInteractiveInputFunction(input.Capability.Function))
        {
            return HasApproval(input, "interactiveInput")
                ? Allow("numadora_policy_allowed_interactive_input", $"{input.Capability.Module}.{input.Capability.Function} is allowed by explicit interactive input approval.")
                : Deny("numadora_policy_interactive_input_not_approved", "Interactive input requires explicit approval.");
        }

        return Deny("numadora_policy_profile_blocked", $"Profile '{input.Capability.Profile}' is not enabled for execution.");
    }

    private static bool IsDangerousClass(string capabilityClass)
    {
        return capabilityClass.Equals("Destructive", StringComparison.OrdinalIgnoreCase)
            || capabilityClass.Equals("Browser-data", StringComparison.OrdinalIgnoreCase)
            || capabilityClass.Equals("Clipboard", StringComparison.OrdinalIgnoreCase)
            || capabilityClass.Equals("Network/remote", StringComparison.OrdinalIgnoreCase)
            || capabilityClass.Equals("Secrets", StringComparison.OrdinalIgnoreCase)
            || capabilityClass.Equals("File-write", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDangerousProfile(string profile)
    {
        return profile.Equals("destructive", StringComparison.OrdinalIgnoreCase)
            || profile.Equals("browser-data", StringComparison.OrdinalIgnoreCase)
            || profile.Equals("secrets", StringComparison.OrdinalIgnoreCase)
            || profile.Equals("unattended", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresTargetIdentity(ScriptCapabilityRequirement capability)
    {
        return capability.CapabilityClass.Equals("User-input", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsApprovedInteractiveInputFunction(string function)
    {
        return function.Equals("Text", StringComparison.OrdinalIgnoreCase)
            || function.Equals("Keys", StringComparison.OrdinalIgnoreCase)
            || function.Equals("Mouse", StringComparison.OrdinalIgnoreCase)
            || function.Equals("Wheel", StringComparison.OrdinalIgnoreCase)
            || function.Equals("Drag", StringComparison.OrdinalIgnoreCase)
            || function.Equals("ContextMenu", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasApproval(NumadoraPolicyInput input, string name)
    {
        if (input.Approvals is null)
        {
            return false;
        }

        foreach (var item in input.Approvals)
        {
            if (item.Key.Equals(name, StringComparison.OrdinalIgnoreCase)
                && item.Value is bool value)
            {
                return value;
            }
        }

        return false;
    }

    private static bool HasSensitiveLineage(NumadoraPolicyInput input)
    {
        return TryLineageValue(input.Lineage, "data", out var data)
            && data is IReadOnlyDictionary<string, object?> dataMap
            && TryLineageValue(dataMap, "classification", out var classification)
            && classification is string value
            && (value.Equals("sensitive", StringComparison.OrdinalIgnoreCase)
                || value.Equals("secret", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryLineageValue(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out object? value)
    {
        foreach (var item in values)
        {
            if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                value = item.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static NumadoraPolicyDecision Allow(string code, string reason)
    {
        return new NumadoraPolicyDecision(true, code, reason);
    }

    private static NumadoraPolicyDecision Deny(string code, string reason)
    {
        return new NumadoraPolicyDecision(false, code, reason);
    }
}
