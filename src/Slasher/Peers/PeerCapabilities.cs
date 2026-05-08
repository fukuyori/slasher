namespace Slasher.Peers;

public static class PeerCapabilities
{
    public const string Hello = "peer.hello";
    public const string CapabilitiesRead = "peer.capabilities.read";
    public const string NamespaceRead = "peer.namespace.read";
    public const string ResourceRead = "peer.resource.read";
    public const string ResourceInvoke = "peer.resource.invoke";
    public const string RunDelegate = "peer.run.delegate";
    public const string RunCancel = "peer.run.cancel";
    public const string ArtifactRead = "peer.artifact.read";
    public const string Relay = "peer.relay";

    public const string ObserveWindowList = "observe.window.list";
    public const string ObserveScreenCapture = "observe.screen.capture";
    public const string ObserveRunRead = "observe.run.read";
    public const string ObserveArtifactRead = "observe.artifact.read";

    public const string WindowFocus = "window.focus";
    public const string WindowMove = "window.move";
    public const string InputText = "input.text";
    public const string InputKeys = "input.keys";
    public const string InputMouse = "input.mouse";
    public const string FileRead = "file.read";
    public const string FileWrite = "file.write";
    public const string FileDelete = "file.delete";
    public const string ClipboardRead = "clipboard.read";
    public const string ClipboardWrite = "clipboard.write";
    public const string BrowserDataRead = "browser.data.read";
    public const string BrowserDataWrite = "browser.data.write";
    public const string Destructive = "destructive";
    public const string Unattended = "unattended";
    public const string Secrets = "secrets";

    public static readonly IReadOnlyList<string> All =
    [
        Hello,
        CapabilitiesRead,
        NamespaceRead,
        ResourceRead,
        ResourceInvoke,
        RunDelegate,
        RunCancel,
        ArtifactRead,
        Relay,
        ObserveWindowList,
        ObserveScreenCapture,
        ObserveRunRead,
        ObserveArtifactRead,
        WindowFocus,
        WindowMove,
        InputText,
        InputKeys,
        InputMouse,
        FileRead,
        FileWrite,
        FileDelete,
        ClipboardRead,
        ClipboardWrite,
        BrowserDataRead,
        BrowserDataWrite,
        Destructive,
        Unattended,
        Secrets
    ];
}
