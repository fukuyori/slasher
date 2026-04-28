using Slasher.Api;

namespace Slasher.Windows;

public sealed partial class WindowsAutomationService
{
    public bool GetElementTree(
        string? handleText,
        int maxDepth,
        int maxChildren,
        out ElementTreeResponse? tree,
        out ErrorResponse? error)
    {
        tree = null;
        var rootHandle = IntPtr.Zero;
        if (string.IsNullOrWhiteSpace(handleText))
        {
            rootHandle = NativeMethods.GetForegroundWindow();
            if (rootHandle == IntPtr.Zero)
            {
                error = new ErrorResponse("foreground_window_not_found", "No foreground window was found.");
                return false;
            }
        }
        else if (!TryResolveWindow(handleText, out rootHandle, out error))
        {
            return false;
        }

        var safeMaxDepth = Math.Clamp(maxDepth, 0, 8);
        var safeMaxChildren = Math.Clamp(maxChildren, 1, 500);
        var context = new ElementTreeContext(safeMaxDepth, safeMaxChildren);
        tree = new ElementTreeResponse(
            BuildElement(rootHandle, 0, context),
            safeMaxDepth,
            safeMaxChildren,
            context.TotalCount,
            context.Truncated);
        error = null;
        return true;
    }

    public bool FindElements(
        string? handleText,
        string? title,
        string? className,
        int? controlId,
        string match,
        int maxDepth,
        int maxResults,
        out ElementFindResponse? response,
        out ErrorResponse? error)
    {
        response = null;
        if (!TryResolveElementRoot(handleText, out var rootHandle, out error))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(title)
            && string.IsNullOrWhiteSpace(className)
            && controlId is null)
        {
            error = new ErrorResponse("element_query_required", "At least one of title, className, or controlId is required.");
            return false;
        }

        var safeMaxDepth = Math.Clamp(maxDepth, 0, 16);
        var safeMaxResults = Math.Clamp(maxResults, 1, 200);
        var exact = match.Equals("exact", StringComparison.OrdinalIgnoreCase);
        var results = new List<WindowElementInfo>();
        var totalScanned = 0;
        var truncated = false;

        WalkElements(rootHandle, 0, safeMaxDepth, element =>
        {
            totalScanned++;
            if (ElementMatches(element, title, className, controlId, exact))
            {
                if (results.Count >= safeMaxResults)
                {
                    truncated = true;
                    return false;
                }

                results.Add(element with { Children = [] });
            }

            return true;
        });

        response = new ElementFindResponse(results, totalScanned, safeMaxDepth, safeMaxResults, truncated);
        error = null;
        return true;
    }

    public bool ClickElement(ElementClickRequest request, out WindowElementInfo? element, out ErrorResponse? error)
    {
        element = null;
        if (!FindElements(
            request.Handle,
            request.Title,
            request.ClassName,
            request.ControlId,
            request.Match,
            request.MaxDepth,
            1,
            out var response,
            out error))
        {
            return false;
        }

        element = response!.Elements.FirstOrDefault();
        if (element is null)
        {
            error = new ErrorResponse("element_not_found", "No matching element was found.");
            return false;
        }

        var x = element.Bounds.X + element.Bounds.Width / 2;
        var y = element.Bounds.Y + element.Bounds.Height / 2;
        return SendMouse(new MouseInputRequest("click", x, y, request.Button), out error);
    }

    public bool ElementExists(
        ElementClickRequest request,
        out ElementExistsResponse? response,
        out ErrorResponse? error)
    {
        response = null;
        if (!FindElements(
            request.Handle,
            request.Title,
            request.ClassName,
            request.ControlId,
            request.Match,
            request.MaxDepth,
            1,
            out var find,
            out error)
            || find is null)
        {
            return false;
        }

        var element = find.Elements.FirstOrDefault();
        response = new ElementExistsResponse(element is not null, element, find.TotalScanned);
        return true;
    }

    public bool GetElementText(
        ElementClickRequest request,
        out ElementTextResponse? response,
        out ErrorResponse? error)
    {
        response = null;
        if (!FindElements(
            request.Handle,
            request.Title,
            request.ClassName,
            request.ControlId,
            request.Match,
            request.MaxDepth,
            1,
            out var find,
            out error)
            || find is null)
        {
            return false;
        }

        var element = find.Elements.FirstOrDefault();
        if (element is null)
        {
            error = new ErrorResponse("element_not_found", "No matching element was found.");
            return false;
        }

        response = new ElementTextResponse(element.Title, element);
        error = null;
        return true;
    }

    private static WindowElementInfo BuildElement(IntPtr handle, int depth, ElementTreeContext context)
    {
        context.TotalCount++;
        var children = new List<WindowElementInfo>();
        if (depth < context.MaxDepth)
        {
            foreach (var child in EnumerateDirectChildren(handle))
            {
                if (children.Count >= context.MaxChildren)
                {
                    context.Truncated = true;
                    break;
                }

                children.Add(BuildElement(child, depth + 1, context));
            }
        }

        _ = NativeMethods.GetWindowRect(handle, out var rect);
        return new WindowElementInfo(
            WindowHandle.Format(handle),
            ReadText(handle, NativeMethods.GetWindowText),
            ReadText(handle, NativeMethods.GetClassName),
            NativeMethods.GetDlgCtrlID(handle),
            new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
            NativeMethods.IsWindowVisible(handle),
            NativeMethods.IsWindowEnabled(handle),
            children);
    }

    private static IReadOnlyList<IntPtr> EnumerateDirectChildren(IntPtr parent)
    {
        var children = new List<IntPtr>();
        NativeMethods.EnumChildWindows(parent, (hwnd, _) =>
        {
            if (NativeMethods.GetParent(hwnd) == parent)
            {
                children.Add(hwnd);
            }

            return true;
        }, IntPtr.Zero);
        return children;
    }

    private static bool TryResolveElementRoot(string? handleText, out IntPtr rootHandle, out ErrorResponse? error)
    {
        rootHandle = IntPtr.Zero;
        if (string.IsNullOrWhiteSpace(handleText))
        {
            rootHandle = NativeMethods.GetForegroundWindow();
            if (rootHandle == IntPtr.Zero)
            {
                error = new ErrorResponse("foreground_window_not_found", "No foreground window was found.");
                return false;
            }

            error = null;
            return true;
        }

        return TryResolveWindow(handleText, out rootHandle, out error);
    }

    private static void WalkElements(IntPtr handle, int depth, int maxDepth, Func<WindowElementInfo, bool> visitor)
    {
        var element = BuildElement(handle, 0, new ElementTreeContext(0, 1));
        if (!visitor(element) || depth >= maxDepth)
        {
            return;
        }

        foreach (var child in EnumerateDirectChildren(handle))
        {
            WalkElements(child, depth + 1, maxDepth, visitor);
        }
    }

    private static bool ElementMatches(
        WindowElementInfo element,
        string? title,
        string? className,
        int? controlId,
        bool exact)
    {
        return MatchesText(element.Title, title, exact)
            && MatchesText(element.ClassName, className, exact)
            && (controlId is null || element.ControlId == controlId.Value);
    }

    private static bool MatchesText(string value, string? query, bool exact)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return exact
            ? value.Equals(query, StringComparison.OrdinalIgnoreCase)
            : value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ElementTreeContext(int maxDepth, int maxChildren)
    {
        public int MaxDepth { get; } = maxDepth;

        public int MaxChildren { get; } = maxChildren;

        public int TotalCount { get; set; }

        public bool Truncated { get; set; }
    }
}
