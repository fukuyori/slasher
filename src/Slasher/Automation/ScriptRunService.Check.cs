using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    public async Task<ScriptCheckResponse> CheckAsync(ScriptCheckRequest request, CancellationToken cancellationToken)
    {
        if (IsRemovedSlasherScript(request.Language, request.Path))
        {
            return RemovedSlasherCheckResponse();
        }

        return await CheckNumadoraAsync(request with { Language = "numadora" }, cancellationToken);
    }

    private static ScriptCheckResponse RemovedSlasherCheckResponse()
    {
        return new ScriptCheckResponse(
            false,
            [RemovedSlasherDiagnostic()],
            [],
            "numadora");
    }

    private static ScriptDiagnostic ToDiagnostic(ScriptCommandException exception)
    {
        return new ScriptDiagnostic(exception.Code, exception.Message);
    }
}
