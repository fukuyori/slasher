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

        if (input.Capability.Module.Equals("slasher_io", StringComparison.OrdinalIgnoreCase))
        {
            return Allow("numadora_policy_allowed_local_observe", "slasher_io host call is allowed in the local observation profile.");
        }

        if (input.Capability.Profile.Equals("observe", StringComparison.OrdinalIgnoreCase))
        {
            return Allow("numadora_policy_allowed_observe", "Observe profile host call is allowed.");
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
