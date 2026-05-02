using Slasher.Automation;
using Xunit;

namespace Slasher.Tests;

public sealed class NumadoraPolicyEvaluatorTests
{
    private readonly NumadoraPolicyEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_AllowsSlasherIoLocalObserveCalls()
    {
        var decision = _evaluator.Evaluate(CreateInput(Capability("slasher_io", "Step", "Observe", "observe")));

        Assert.True(decision.Allow);
        Assert.Equal("numadora_policy_allowed_local_observe", decision.Code);
    }

    [Fact]
    public void Evaluate_DeniesMissingCapability()
    {
        var decision = _evaluator.Evaluate(CreateInput(capability: null));

        Assert.False(decision.Allow);
        Assert.Equal("numadora_policy_missing_capability", decision.Code);
    }

    [Fact]
    public void Evaluate_DeniesMissingPurpose()
    {
        var decision = _evaluator.Evaluate(
            CreateInput(Capability("slasher_io", "Step", "Observe", "observe"), purpose: ""));

        Assert.False(decision.Allow);
        Assert.Equal("numadora_policy_missing_purpose", decision.Code);
    }

    [Theory]
    [InlineData("File-write", "observe")]
    [InlineData("Secrets", "observe")]
    [InlineData("Observe", "destructive")]
    public void Evaluate_DeniesDangerousCapabilityClassesAndProfiles(string capabilityClass, string profile)
    {
        var decision = _evaluator.Evaluate(
            CreateInput(Capability("slasher_file", "WriteText", capabilityClass, profile)));

        Assert.False(decision.Allow);
        Assert.Equal("numadora_policy_capability_blocked", decision.Code);
    }

    [Fact]
    public void Evaluate_DeniesSensitiveLineage()
    {
        var lineage = new Dictionary<string, object?>
        {
            ["data"] = new Dictionary<string, object?>
            {
                ["classification"] = "sensitive",
            },
        };

        var decision = _evaluator.Evaluate(
            CreateInput(Capability("slasher_io", "Log", "Observe", "observe"), lineage: lineage));

        Assert.False(decision.Allow);
        Assert.Equal("numadora_policy_sensitive_lineage", decision.Code);
    }

    [Fact]
    public void Evaluate_DeniesInteractiveProfileUntilExecutionPolicyExists()
    {
        var decision = _evaluator.Evaluate(
            CreateInput(Capability("slasher_input", "Text", "User-input", "interactive")));

        Assert.False(decision.Allow);
        Assert.Equal("numadora_policy_profile_blocked", decision.Code);
    }

    private static NumadoraPolicyInput CreateInput(
        ScriptCapabilityRequirement? capability,
        string purpose = "local-test",
        IReadOnlyDictionary<string, object?>? lineage = null)
    {
        return new NumadoraPolicyInput(
            Language: "numadora",
            RunId: "run-test",
            Purpose: purpose,
            Surface: "test",
            Capability: capability,
            HostCall: new NumadoraPolicyHostCall(
                capability?.Module ?? "unknown",
                capability?.Function ?? "Unknown",
                Array.Empty<string>()),
            Lineage: lineage ?? new Dictionary<string, object?>
            {
                ["data"] = new Dictionary<string, object?>
                {
                    ["classification"] = "local",
                },
            });
    }

    private static ScriptCapabilityRequirement Capability(
        string module,
        string function,
        string capabilityClass,
        string profile)
    {
        return new ScriptCapabilityRequirement(
            module,
            function,
            capabilityClass,
            profile,
            "test capability");
    }
}
