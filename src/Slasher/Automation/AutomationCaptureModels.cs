namespace Slasher.Automation;
public sealed record CapturePolicy(
    bool CaptureOnError = true,
    bool CaptureOnAssertionFailure = true,
    bool CaptureAfterEachStep = false,
    bool CaptureBeforeEachStep = false,
    string CaptureTarget = "selected",
    string ImageFormat = "bmp");

