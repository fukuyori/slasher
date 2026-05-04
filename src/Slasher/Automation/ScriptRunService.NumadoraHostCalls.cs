using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<NumadoraLocalHostCallResult> ExecuteNumadoraLocalHostCallAsync(
        NumadoraHostCall hostCall,
        NumadoraPolicyInput policyInput,
        NumadoraPolicyDecision policyDecision,
        CancellationToken cancellationToken)
    {
        if (!policyDecision.Allow)
        {
            return NumadoraLocalHostCallResult.Failed(
                "numadora_policy_denied",
                policyDecision.Reason,
                executedBy: "slasher-policy",
                target: policyInput.Target);
        }

        if (hostCall.Module.Equals("slasher_io", StringComparison.OrdinalIgnoreCase))
        {
            return NumadoraLocalHostCallResult.Passed(new
            {
                Observed = true,
                ExecutedBy = "numadora-stub",
                PolicyAllowed = true,
                PolicyCode = policyDecision.Code
            });
        }

        var dialogResult = ExecuteNumadoraDialogHostCall(hostCall, policyDecision);
        if (dialogResult is not null)
        {
            return dialogResult;
        }

        if (hostCall.Module.Equals("slasher_window", StringComparison.OrdinalIgnoreCase)
            && hostCall.Function.Equals("WaitForTitle", StringComparison.OrdinalIgnoreCase))
        {
            var (title, timeoutMs) = ParseNumadoraTitleAndOptionalTimeout(hostCall.Arguments, defaultTimeoutMs: 10000);
            if (string.IsNullOrWhiteSpace(title))
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_host_call_invalid_arguments",
                    "slasher_window.WaitForTitle requires a title.",
                    executedBy: "slasher-window");
            }

            var window = await _automation.WaitForWindowAsync(
                new WindowQueryRequest(title, TimeoutMs: timeoutMs),
                cancellationToken);
            if (window is null)
            {
                return NumadoraLocalHostCallResult.Failed(
                    "window_not_found",
                    $"No window containing '{title}' was found before the timeout.",
                    executedBy: "slasher-window",
                    expected: new { exists = true, title, timeoutMs },
                    actual: new { exists = false });
            }

            return NumadoraLocalHostCallResult.Passed(
                new
                {
                    observed = true,
                    title = window.Title,
                    handle = window.Handle,
                    timeoutMs,
                    executedBy = "slasher-window",
                    policyAllowed = true,
                    policyCode = policyDecision.Code
                },
                ToTarget(window),
                "slasher-window");
        }

        if (hostCall.Module.Equals("slasher_app", StringComparison.OrdinalIgnoreCase)
            && hostCall.Function.Equals("Start", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = string.Join(' ', hostCall.Arguments).Trim();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_host_call_invalid_arguments",
                    "slasher_app.Start requires a file name.",
                    executedBy: "slasher-app");
            }

            try
            {
                var result = _automation.StartApp(new StartAppRequest(fileName));
                var target = result.MainWindowHandle is null
                    ? null
                    : new AutomationTarget(
                        "window",
                        result.MainWindowHandle,
                        result.MainWindowTitle,
                        ProcessId: result.ProcessId,
                        ProcessName: result.ProcessName);
                return NumadoraLocalHostCallResult.Passed(
                    new
                    {
                        started = true,
                        fileName,
                        result.ProcessId,
                        result.ProcessName,
                        result.MainWindowHandle,
                        result.MainWindowTitle,
                        executedBy = "slasher-app",
                        policyAllowed = true,
                        policyCode = policyDecision.Code
                    },
                    target,
                    "slasher-app");
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
            {
                return NumadoraLocalHostCallResult.Failed(
                    "app_start_failed",
                    ex.Message,
                    executedBy: "slasher-app",
                    expected: new { started = true, fileName },
                    actual: new { started = false, error = ex.GetType().Name });
            }
        }

        if (hostCall.Module.Equals("slasher_window", StringComparison.OrdinalIgnoreCase)
            && hostCall.Function.Equals("Focus", StringComparison.OrdinalIgnoreCase))
        {
            var handle = string.Join(' ', hostCall.Arguments).Trim();
            if (string.IsNullOrWhiteSpace(handle))
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_host_call_invalid_arguments",
                    "slasher_window.Focus requires a window handle.",
                    executedBy: "slasher-window");
            }

            var target = new AutomationTarget("window", Handle: handle);
            if (!_automation.FocusWindow(handle, out var error))
            {
                return NumadoraLocalHostCallResult.Failed(
                    error?.Code ?? "focus_failed",
                    error?.Message ?? "Failed to focus the target window.",
                    executedBy: "slasher-window",
                    target: target,
                    expected: new { focused = true, handle },
                    actual: new { focused = false });
            }

            var refreshed = _automation.TryGetWindow(handle, out var window) ? ToTarget(window) : target;
            return NumadoraLocalHostCallResult.Passed(
                new
                {
                    focused = true,
                    handle,
                    executedBy = "slasher-window",
                    policyAllowed = true,
                    policyCode = policyDecision.Code
                },
                refreshed,
                "slasher-window");
        }

        if (hostCall.Module.Equals("slasher_input", StringComparison.OrdinalIgnoreCase)
            && hostCall.Function.Equals("Text", StringComparison.OrdinalIgnoreCase))
        {
            var text = string.Join(' ', hostCall.Arguments);
            var currentTarget = _automation.TryGetForegroundWindow(out var foreground) && foreground is not null
                ? ToTarget(foreground)
                : null;
            if (!TargetMatches(policyInput.Target, currentTarget))
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_policy_target_changed",
                    "Foreground target changed before sending text input.",
                    executedBy: "slasher-input",
                    target: policyInput.Target,
                    expected: policyInput.Target,
                    actual: currentTarget);
            }

            if (!_automation.SendText(new TextInputRequest(text), out var error))
            {
                return NumadoraLocalHostCallResult.Failed(
                    error?.Code ?? "text_failed",
                    error?.Message ?? "Failed to send text input.",
                    executedBy: "slasher-input",
                    target: policyInput.Target,
                    expected: new { sent = true, chars = text.Length },
                    actual: new { sent = false });
            }

            return NumadoraLocalHostCallResult.Passed(
                new
                {
                    sent = true,
                    chars = text.Length,
                    executedBy = "slasher-input",
                    policyAllowed = true,
                    policyCode = policyDecision.Code,
                    targetRevalidated = true
                },
                currentTarget ?? policyInput.Target,
                "slasher-input");
        }

        if (hostCall.Module.Equals("slasher_input", StringComparison.OrdinalIgnoreCase)
            && hostCall.Function.Equals("Keys", StringComparison.OrdinalIgnoreCase))
        {
            var keys = string.Join('+', hostCall.Arguments);
            var currentTarget = _automation.TryGetForegroundWindow(out var foreground) && foreground is not null
                ? ToTarget(foreground)
                : null;
            if (!TargetMatches(policyInput.Target, currentTarget))
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_policy_target_changed",
                    "Foreground target changed before sending key input.",
                    executedBy: "slasher-input",
                    target: policyInput.Target,
                    expected: policyInput.Target,
                    actual: currentTarget);
            }

            if (!_automation.SendKeys(new KeyInputRequest(keys), out var error))
            {
                return NumadoraLocalHostCallResult.Failed(
                    error?.Code ?? "keys_failed",
                    error?.Message ?? "Failed to send key input.",
                    executedBy: "slasher-input",
                    target: policyInput.Target,
                    expected: new { sent = true, keys },
                    actual: new { sent = false });
            }

            return NumadoraLocalHostCallResult.Passed(
                new
                {
                    sent = true,
                    keys,
                    executedBy = "slasher-input",
                    policyAllowed = true,
                    policyCode = policyDecision.Code,
                    targetRevalidated = true
                },
                currentTarget ?? policyInput.Target,
                "slasher-input");
        }

        if (hostCall.Module.Equals("slasher_input", StringComparison.OrdinalIgnoreCase)
            && hostCall.Function.Equals("Mouse", StringComparison.OrdinalIgnoreCase))
        {
            var mouse = ParseNumadoraMouseArgs(hostCall.Arguments);
            if (mouse is null)
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_host_call_invalid_arguments",
                    "slasher_input.Mouse requires action, x, y, and button.",
                    executedBy: "slasher-input");
            }

            var currentTarget = _automation.TryGetForegroundWindow(out var foreground) && foreground is not null
                ? ToTarget(foreground)
                : null;
            if (!TargetMatches(policyInput.Target, currentTarget))
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_policy_target_changed",
                    "Foreground target changed before sending mouse input.",
                    executedBy: "slasher-input",
                    target: policyInput.Target,
                    expected: policyInput.Target,
                    actual: currentTarget);
            }

            if (!_automation.SendMouse(new MouseInputRequest(mouse.Value.Action, mouse.Value.X, mouse.Value.Y, mouse.Value.Button), out var error))
            {
                return NumadoraLocalHostCallResult.Failed(
                    error?.Code ?? "mouse_failed",
                    error?.Message ?? "Failed to send mouse input.",
                    executedBy: "slasher-input",
                    target: policyInput.Target,
                    expected: new { sent = true, mouse.Value.Action, mouse.Value.X, mouse.Value.Y, mouse.Value.Button },
                    actual: new { sent = false });
            }

            return NumadoraLocalHostCallResult.Passed(
                new
                {
                    sent = true,
                    mouse.Value.Action,
                    mouse.Value.X,
                    mouse.Value.Y,
                    mouse.Value.Button,
                    executedBy = "slasher-input",
                    policyAllowed = true,
                    policyCode = policyDecision.Code,
                    targetRevalidated = true
                },
                currentTarget ?? policyInput.Target,
                "slasher-input");
        }

        if (hostCall.Module.Equals("slasher_input", StringComparison.OrdinalIgnoreCase)
            && hostCall.Function.Equals("Wheel", StringComparison.OrdinalIgnoreCase))
        {
            var wheel = ParseNumadoraWheelArgs(hostCall.Arguments);
            if (wheel is null)
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_host_call_invalid_arguments",
                    "slasher_input.Wheel requires x, y, and delta.",
                    executedBy: "slasher-input");
            }

            var currentTarget = _automation.TryGetForegroundWindow(out var foreground) && foreground is not null
                ? ToTarget(foreground)
                : null;
            if (!TargetMatches(policyInput.Target, currentTarget))
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_policy_target_changed",
                    "Foreground target changed before sending mouse wheel input.",
                    executedBy: "slasher-input",
                    target: policyInput.Target,
                    expected: policyInput.Target,
                    actual: currentTarget);
            }

            if (!_automation.SendMouse(new MouseInputRequest("wheel", wheel.Value.X, wheel.Value.Y, WheelDelta: wheel.Value.Delta), out var error))
            {
                return NumadoraLocalHostCallResult.Failed(
                    error?.Code ?? "mouse_wheel_failed",
                    error?.Message ?? "Failed to send mouse wheel input.",
                    executedBy: "slasher-input",
                    target: policyInput.Target,
                    expected: new { sent = true, wheel.Value.X, wheel.Value.Y, wheel.Value.Delta },
                    actual: new { sent = false });
            }

            return NumadoraLocalHostCallResult.Passed(
                new
                {
                    sent = true,
                    wheel.Value.X,
                    wheel.Value.Y,
                    wheel.Value.Delta,
                    executedBy = "slasher-input",
                    policyAllowed = true,
                    policyCode = policyDecision.Code,
                    targetRevalidated = true
                },
                currentTarget ?? policyInput.Target,
                "slasher-input");
        }

        if (hostCall.Module.Equals("slasher_input", StringComparison.OrdinalIgnoreCase)
            && hostCall.Function.Equals("Drag", StringComparison.OrdinalIgnoreCase))
        {
            var drag = ParseNumadoraDragArgs(hostCall.Arguments);
            if (drag is null)
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_host_call_invalid_arguments",
                    "slasher_input.Drag requires fromX, fromY, toX, toY, button, durationMs, and steps.",
                    executedBy: "slasher-input");
            }

            var currentTarget = _automation.TryGetForegroundWindow(out var foreground) && foreground is not null
                ? ToTarget(foreground)
                : null;
            if (!TargetMatches(policyInput.Target, currentTarget))
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_policy_target_changed",
                    "Foreground target changed before sending mouse drag input.",
                    executedBy: "slasher-input",
                    target: policyInput.Target,
                    expected: policyInput.Target,
                    actual: currentTarget);
            }

            if (!_automation.DragMouse(
                new MouseDragRequest(
                    drag.Value.FromX,
                    drag.Value.FromY,
                    drag.Value.ToX,
                    drag.Value.ToY,
                    drag.Value.Button,
                    drag.Value.DurationMs,
                    drag.Value.Steps),
                out var error))
            {
                return NumadoraLocalHostCallResult.Failed(
                    error?.Code ?? "mouse_drag_failed",
                    error?.Message ?? "Failed to send mouse drag input.",
                    executedBy: "slasher-input",
                    target: policyInput.Target,
                    expected: new { sent = true, drag.Value.FromX, drag.Value.FromY, drag.Value.ToX, drag.Value.ToY, drag.Value.Button },
                    actual: new { sent = false });
            }

            return NumadoraLocalHostCallResult.Passed(
                new
                {
                    sent = true,
                    drag.Value.FromX,
                    drag.Value.FromY,
                    drag.Value.ToX,
                    drag.Value.ToY,
                    drag.Value.Button,
                    drag.Value.DurationMs,
                    drag.Value.Steps,
                    executedBy = "slasher-input",
                    policyAllowed = true,
                    policyCode = policyDecision.Code,
                    targetRevalidated = true
                },
                currentTarget ?? policyInput.Target,
                "slasher-input");
        }

        if (hostCall.Module.Equals("slasher_input", StringComparison.OrdinalIgnoreCase)
            && hostCall.Function.Equals("ContextMenu", StringComparison.OrdinalIgnoreCase))
        {
            var contextMenu = ParseNumadoraContextMenuArgs(hostCall.Arguments);
            if (contextMenu is null)
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_host_call_invalid_arguments",
                    "slasher_input.ContextMenu requires x, y, and delayMs.",
                    executedBy: "slasher-input");
            }

            var currentTarget = _automation.TryGetForegroundWindow(out var foreground) && foreground is not null
                ? ToTarget(foreground)
                : null;
            if (!TargetMatches(policyInput.Target, currentTarget))
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_policy_target_changed",
                    "Foreground target changed before opening context menu.",
                    executedBy: "slasher-input",
                    target: policyInput.Target,
                    expected: policyInput.Target,
                    actual: currentTarget);
            }

            if (!_automation.GetContextMenu(
                new ContextMenuRequest(contextMenu.Value.X, contextMenu.Value.Y, contextMenu.Value.DelayMs),
                out var response,
                out var error)
                || response is null)
            {
                return NumadoraLocalHostCallResult.Failed(
                    error?.Code ?? "context_menu_failed",
                    error?.Message ?? "Failed to open context menu.",
                    executedBy: "slasher-input",
                    target: policyInput.Target,
                    expected: new { opened = true, contextMenu.Value.X, contextMenu.Value.Y, contextMenu.Value.DelayMs },
                    actual: new { opened = false });
            }

            var observedTarget = response.ForegroundWindow is null
                ? currentTarget ?? policyInput.Target
                : ToTarget(response.ForegroundWindow);
            return NumadoraLocalHostCallResult.Passed(
                new
                {
                    opened = true,
                    response.X,
                    response.Y,
                    contextMenu.Value.DelayMs,
                    response.Observation,
                    screenshot = new
                    {
                        response.Screenshot.MimeType,
                        response.Screenshot.Width,
                        response.Screenshot.Height
                    },
                    executedBy = "slasher-input",
                    policyAllowed = true,
                    policyCode = policyDecision.Code,
                    targetRevalidated = true
                },
                observedTarget,
                "slasher-input");
        }

        if (hostCall.Module.Equals("slasher_screen", StringComparison.OrdinalIgnoreCase)
            && hostCall.Function.Equals("Capture", StringComparison.OrdinalIgnoreCase))
        {
            var capture = ParseNumadoraCaptureArgs(hostCall.Arguments);
            if (capture is null)
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_host_call_invalid_arguments",
                    "slasher_screen.Capture requires scope ('full' or 'selected'), maxWidth, and maxHeight.",
                    executedBy: "slasher-screen");
            }

            var handle = capture.Value.Scope.Equals("full", StringComparison.OrdinalIgnoreCase)
                ? null
                : policyInput.Target?.Handle;
            if (capture.Value.Scope.Equals("selected", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(handle))
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_policy_missing_target",
                    "slasher_screen.Capture('selected', ...) requires an observable target handle.",
                    executedBy: "slasher-screen",
                    target: policyInput.Target);
            }

            if (!_automation.TakeScreenshot(
                new ScreenshotRequest(
                    handle,
                    MaxWidth: capture.Value.MaxWidth <= 0 ? null : capture.Value.MaxWidth,
                    MaxHeight: capture.Value.MaxHeight <= 0 ? null : capture.Value.MaxHeight),
                out var screenshot,
                out var error)
                || screenshot is null)
            {
                return NumadoraLocalHostCallResult.Failed(
                    error?.Code ?? "capture_failed",
                    error?.Message ?? "Failed to capture the screen.",
                    executedBy: "slasher-screen",
                    target: policyInput.Target,
                    expected: new { captured = true, capture.Value.Scope, capture.Value.MaxWidth, capture.Value.MaxHeight },
                    actual: new { captured = false });
            }

            return NumadoraLocalHostCallResult.Passed(
                new
                {
                    captured = true,
                    capture.Value.Scope,
                    screenshot.MimeType,
                    screenshot.Width,
                    screenshot.Height,
                    executedBy = "slasher-screen",
                    policyAllowed = true,
                    policyCode = policyDecision.Code
                },
                policyInput.Target,
                "slasher-screen",
                screenshot);
        }

        if (hostCall.Module.Equals("slasher_element", StringComparison.OrdinalIgnoreCase)
            && hostCall.Function.Equals("Tree", StringComparison.OrdinalIgnoreCase))
        {
            var tree = ParseNumadoraElementTreeArgs(hostCall.Arguments);
            if (tree is null)
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_host_call_invalid_arguments",
                    "slasher_element.Tree requires scope, maxDepth, and maxChildren.",
                    executedBy: "slasher-element");
            }

            if (!TryResolveNumadoraElementScope(tree.Value.Scope, policyInput.Target, out var handle, out var scopeError))
            {
                return scopeError;
            }

            if (!_automation.GetElementTree(handle, tree.Value.MaxDepth, tree.Value.MaxChildren, out var response, out var error)
                || response is null)
            {
                return NumadoraLocalHostCallResult.Failed(
                    error?.Code ?? "element_tree_failed",
                    error?.Message ?? "Failed to capture the element tree.",
                    executedBy: "slasher-element",
                    target: policyInput.Target,
                    expected: new { captured = true, tree.Value.Scope, tree.Value.MaxDepth, tree.Value.MaxChildren },
                    actual: new { captured = false });
            }

            return NumadoraLocalHostCallResult.Passed(
                new
                {
                    captured = true,
                    tree.Value.Scope,
                    response.MaxDepth,
                    response.MaxChildren,
                    response.TotalCount,
                    response.Truncated,
                    Root = response.Root,
                    executedBy = "slasher-element",
                    policyAllowed = true,
                    policyCode = policyDecision.Code
                },
                policyInput.Target,
                "slasher-element");
        }

        if (hostCall.Module.Equals("slasher_element", StringComparison.OrdinalIgnoreCase)
            && (hostCall.Function.Equals("Find", StringComparison.OrdinalIgnoreCase)
                || hostCall.Function.Equals("Exists", StringComparison.OrdinalIgnoreCase)
                || hostCall.Function.Equals("ReadText", StringComparison.OrdinalIgnoreCase)))
        {
            var query = ParseNumadoraElementQueryArgs(hostCall.Arguments);
            if (query is null)
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_host_call_invalid_arguments",
                    "slasher_element query calls require scope, title, className, controlId, match, maxDepth, and maxResults.",
                    executedBy: "slasher-element");
            }

            if (!TryResolveNumadoraElementScope(query.Value.Scope, policyInput.Target, out var handle, out var scopeError))
            {
                return scopeError;
            }

            if (hostCall.Function.Equals("Locate", StringComparison.OrdinalIgnoreCase))
            {
                if (!_automation.FindElements(
                    handle,
                    query.Value.Title,
                    query.Value.ClassName,
                    query.Value.ControlId,
                    query.Value.Match,
                    query.Value.MaxDepth,
                    query.Value.MaxResults,
                    out var response,
                    out var error)
                    || response is null)
                {
                    return NumadoraLocalHostCallResult.Failed(
                        error?.Code ?? "element_find_failed",
                        error?.Message ?? "Failed to find elements.",
                        executedBy: "slasher-element",
                        target: policyInput.Target);
                }

                return NumadoraLocalHostCallResult.Passed(
                    new
                    {
                        found = response.Elements.Count > 0,
                        response.Elements,
                        response.TotalScanned,
                        response.MaxDepth,
                        response.MaxResults,
                        response.Truncated,
                        executedBy = "slasher-element",
                        policyAllowed = true,
                        policyCode = policyDecision.Code
                    },
                    policyInput.Target,
                    "slasher-element");
            }

            var request = new ElementClickRequest(
                handle,
                query.Value.Title,
                query.Value.ClassName,
                query.Value.ControlId,
                query.Value.Match,
                query.Value.MaxDepth);

            if (hostCall.Function.Equals("Exists", StringComparison.OrdinalIgnoreCase))
            {
                if (!_automation.ElementExists(request, out var response, out var error) || response is null)
                {
                    return NumadoraLocalHostCallResult.Failed(
                        error?.Code ?? "element_exists_failed",
                        error?.Message ?? "Failed to check element existence.",
                        executedBy: "slasher-element",
                        target: policyInput.Target);
                }

                return NumadoraLocalHostCallResult.Passed(
                    new
                    {
                        response.Exists,
                        response.Element,
                        response.TotalScanned,
                        executedBy = "slasher-element",
                        policyAllowed = true,
                        policyCode = policyDecision.Code
                    },
                    policyInput.Target,
                    "slasher-element");
            }

            if (!_automation.GetElementText(request, out var textResponse, out var textError) || textResponse is null)
            {
                return NumadoraLocalHostCallResult.Failed(
                    textError?.Code ?? "element_text_failed",
                    textError?.Message ?? "Failed to read element text.",
                    executedBy: "slasher-element",
                    target: policyInput.Target);
            }

            return NumadoraLocalHostCallResult.Passed(
                new
                {
                    textResponse.Text,
                    textResponse.Element,
                    executedBy = "slasher-element",
                    policyAllowed = true,
                    policyCode = policyDecision.Code
                },
                policyInput.Target,
                "slasher-element");
        }

        if (hostCall.Module.Equals("slasher_browser", StringComparison.OrdinalIgnoreCase))
        {
            var browser = ExecuteNumadoraBrowserObserveCall(hostCall, policyDecision);
            if (browser is not null)
            {
                return browser;
            }
        }

        if (hostCall.Module.Equals("slasher_test", StringComparison.OrdinalIgnoreCase)
            && hostCall.Function.Equals("AssertForegroundTitle", StringComparison.OrdinalIgnoreCase))
        {
            var (op, expected) = ParseNumadoraOperatorAndExpected(hostCall.Arguments);
            if (string.IsNullOrWhiteSpace(op) || string.IsNullOrWhiteSpace(expected))
            {
                return NumadoraLocalHostCallResult.Failed(
                    "numadora_host_call_invalid_arguments",
                    "slasher_test.AssertForegroundTitle requires an operator and expected text.",
                    executedBy: "slasher-test");
            }

            if (!_automation.TryGetForegroundWindow(out var window) || window is null)
            {
                return NumadoraLocalHostCallResult.Failed(
                    "foreground_window_not_found",
                    "No foreground window was found.",
                    executedBy: "slasher-test");
            }

            var actual = window.Title ?? string.Empty;
            if (!MatchesNumadoraTitleAssertion(actual, op, expected))
            {
                return NumadoraLocalHostCallResult.Failed(
                    "assertion_failed",
                    $"Title assertion failed. Expected title {op} '{expected}', actual '{actual}'.",
                    executedBy: "slasher-test",
                    target: ToTarget(window),
                    expected: new { title = expected, op },
                    actual: new { title = actual, window.Handle });
            }

            return NumadoraLocalHostCallResult.Passed(
                new
                {
                    asserted = true,
                    title = actual,
                    op,
                    expected,
                    executedBy = "slasher-test",
                    policyAllowed = true,
                    policyCode = policyDecision.Code
                },
                ToTarget(window),
                "slasher-test");
        }

        return NumadoraLocalHostCallResult.Failed(
            "numadora_host_call_not_enabled",
            $"Host call '{hostCall.Module}.{hostCall.Function}' is not enabled for local execution.",
            executedBy: "slasher-host");
    }

    private NumadoraLocalHostCallResult? ExecuteNumadoraBrowserObserveCall(
        NumadoraHostCall hostCall,
        NumadoraPolicyDecision policyDecision)
    {
        try
        {
            if (hostCall.Function.Equals("Current", StringComparison.OrdinalIgnoreCase))
            {
                var session = _browser.Current(ParseOptionalSessionId(hostCall.Arguments));
                return NumadoraLocalHostCallResult.Passed(
                    BrowserResult(session, policyDecision),
                    executedBy: "slasher-browser");
            }

            if (hostCall.Function.Equals("Title", StringComparison.OrdinalIgnoreCase))
            {
                var value = _browser.Title(ParseOptionalSessionId(hostCall.Arguments));
                return NumadoraLocalHostCallResult.Passed(
                    BrowserValueResult(value, policyDecision),
                    executedBy: "slasher-browser");
            }

            if (hostCall.Function.Equals("Url", StringComparison.OrdinalIgnoreCase))
            {
                var value = _browser.Url(ParseOptionalSessionId(hostCall.Arguments));
                return NumadoraLocalHostCallResult.Passed(
                    BrowserValueResult(value, policyDecision),
                    executedBy: "slasher-browser");
            }

            if (hostCall.Function.Equals("Links", StringComparison.OrdinalIgnoreCase))
            {
                var links = _browser.Links(ParseOptionalSessionId(hostCall.Arguments));
                return NumadoraLocalHostCallResult.Passed(
                    new
                    {
                        links,
                        count = links.Count,
                        executedBy = "slasher-browser",
                        policyAllowed = true,
                        policyCode = policyDecision.Code
                    },
                    executedBy: "slasher-browser");
            }

            if (hostCall.Function.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                var windows = _browser.Windows(ParseOptionalSessionId(hostCall.Arguments));
                return NumadoraLocalHostCallResult.Passed(
                    new
                    {
                        windows,
                        count = windows.Count,
                        executedBy = "slasher-browser",
                        policyAllowed = true,
                        policyCode = policyDecision.Code
                    },
                    executedBy: "slasher-browser");
            }

            if (hostCall.Function.Equals("Screenshot", StringComparison.OrdinalIgnoreCase))
            {
                var screenshot = _browser.Screenshot(new BrowserScreenshotRequest(ParseOptionalSessionId(hostCall.Arguments)));
                return NumadoraLocalHostCallResult.Passed(
                    new
                    {
                        screenshot.MimeType,
                        screenshot.Width,
                        screenshot.Height,
                        executedBy = "slasher-browser",
                        policyAllowed = true,
                        policyCode = policyDecision.Code
                    },
                    executedBy: "slasher-browser",
                    screenshot: screenshot);
            }

            if (hostCall.Function.Equals("Locate", StringComparison.OrdinalIgnoreCase))
            {
                var selector = ParseNumadoraBrowserSelectorArgs(hostCall.Arguments);
                if (selector is null)
                {
                    return NumadoraBrowserInvalidSelectorResult();
                }

                var element = _browser.Find(new BrowserSelectorRequest(
                    selector.Value.Using,
                    selector.Value.Value,
                    ParseOptionalSessionId(selector.Value.SessionId),
                    selector.Value.TimeoutMs));
                return NumadoraLocalHostCallResult.Passed(
                    new
                    {
                        element,
                        executedBy = "slasher-browser",
                        policyAllowed = true,
                        policyCode = policyDecision.Code
                    },
                    executedBy: "slasher-browser");
            }

            if (hostCall.Function.Equals("DomText", StringComparison.OrdinalIgnoreCase))
            {
                var selector = ParseNumadoraBrowserSelectorArgs(hostCall.Arguments);
                if (selector is null)
                {
                    return NumadoraBrowserInvalidSelectorResult();
                }

                var value = _browser.Text(new BrowserSelectorRequest(
                    selector.Value.Using,
                    selector.Value.Value,
                    ParseOptionalSessionId(selector.Value.SessionId),
                    selector.Value.TimeoutMs));
                return NumadoraLocalHostCallResult.Passed(
                    BrowserValueResult(value, policyDecision),
                    executedBy: "slasher-browser");
            }

            if (hostCall.Function.Equals("Attribute", StringComparison.OrdinalIgnoreCase))
            {
                var attribute = ParseNumadoraBrowserAttributeArgs(hostCall.Arguments);
                if (attribute is null)
                {
                    return NumadoraLocalHostCallResult.Failed(
                        "numadora_host_call_invalid_arguments",
                        "slasher_browser.Attribute requires using, value, attribute, timeoutMs, and sessionId.",
                        executedBy: "slasher-browser");
                }

                var value = _browser.Attribute(new BrowserAttributeRequest(
                    attribute.Value.Using,
                    attribute.Value.Value,
                    attribute.Value.Attribute,
                    ParseOptionalSessionId(attribute.Value.SessionId),
                    attribute.Value.TimeoutMs));
                return NumadoraLocalHostCallResult.Passed(
                    BrowserValueResult(value, policyDecision),
                    executedBy: "slasher-browser");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or OpenQA.Selenium.WebDriverException)
        {
            return NumadoraLocalHostCallResult.Failed(
                "browser_observe_failed",
                ex.Message,
                executedBy: "slasher-browser");
        }

        return null;
    }

    private static bool TargetMatches(AutomationTarget? expected, AutomationTarget? actual)
    {
        if (expected is null || actual is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expected.Handle) && !string.IsNullOrWhiteSpace(actual.Handle))
        {
            return string.Equals(expected.Handle, actual.Handle, StringComparison.OrdinalIgnoreCase);
        }

        if (expected.ProcessId is not null && actual.ProcessId is not null)
        {
            return expected.ProcessId == actual.ProcessId;
        }

        if (!string.IsNullOrWhiteSpace(expected.Title) && !string.IsNullOrWhiteSpace(actual.Title))
        {
            return string.Equals(expected.Title, actual.Title, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static NumadoraMouseArgs? ParseNumadoraMouseArgs(IReadOnlyList<string> args)
    {
        var tokens = string.Join(' ', args)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 4 || !int.TryParse(tokens[1], out var x) || !int.TryParse(tokens[2], out var y))
        {
            return null;
        }

        return new NumadoraMouseArgs(tokens[0], x, y, tokens[3]);
    }

    private static NumadoraWheelArgs? ParseNumadoraWheelArgs(IReadOnlyList<string> args)
    {
        var tokens = string.Join(' ', args)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 3
            || !int.TryParse(tokens[0], out var x)
            || !int.TryParse(tokens[1], out var y)
            || !int.TryParse(tokens[2], out var delta))
        {
            return null;
        }

        return new NumadoraWheelArgs(x, y, delta);
    }

    private static NumadoraDragArgs? ParseNumadoraDragArgs(IReadOnlyList<string> args)
    {
        var tokens = string.Join(' ', args)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 7
            || !int.TryParse(tokens[0], out var fromX)
            || !int.TryParse(tokens[1], out var fromY)
            || !int.TryParse(tokens[2], out var toX)
            || !int.TryParse(tokens[3], out var toY)
            || !int.TryParse(tokens[5], out var durationMs)
            || !int.TryParse(tokens[6], out var steps))
        {
            return null;
        }

        return new NumadoraDragArgs(fromX, fromY, toX, toY, tokens[4], durationMs, steps);
    }

    private static NumadoraContextMenuArgs? ParseNumadoraContextMenuArgs(IReadOnlyList<string> args)
    {
        var tokens = string.Join(' ', args)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 3
            || !int.TryParse(tokens[0], out var x)
            || !int.TryParse(tokens[1], out var y)
            || !int.TryParse(tokens[2], out var delayMs))
        {
            return null;
        }

        return new NumadoraContextMenuArgs(x, y, delayMs);
    }

    private static NumadoraCaptureArgs? ParseNumadoraCaptureArgs(IReadOnlyList<string> args)
    {
        var tokens = string.Join(' ', args)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 3
            || (!tokens[0].Equals("full", StringComparison.OrdinalIgnoreCase)
                && !tokens[0].Equals("selected", StringComparison.OrdinalIgnoreCase))
            || !int.TryParse(tokens[1], out var maxWidth)
            || !int.TryParse(tokens[2], out var maxHeight))
        {
            return null;
        }

        return new NumadoraCaptureArgs(tokens[0].ToLowerInvariant(), maxWidth, maxHeight);
    }

    private static NumadoraElementTreeArgs? ParseNumadoraElementTreeArgs(IReadOnlyList<string> args)
    {
        var tokens = SplitNumadoraArgs(args);
        if (tokens.Length < 3
            || !int.TryParse(tokens[1], out var maxDepth)
            || !int.TryParse(tokens[2], out var maxChildren)
            || maxDepth < 0
            || maxChildren < 1)
        {
            return null;
        }

        return new NumadoraElementTreeArgs(tokens[0], maxDepth, maxChildren);
    }

    private static NumadoraElementQueryArgs? ParseNumadoraElementQueryArgs(IReadOnlyList<string> args)
    {
        var tokens = SplitNumadoraArgs(args);
        if (tokens.Length < 7
            || !int.TryParse(tokens[3], out var controlId)
            || !int.TryParse(tokens[5], out var maxDepth)
            || !int.TryParse(tokens[6], out var maxResults)
            || (tokens[4] is not "contains" and not "exact"))
        {
            return null;
        }

        var title = PlaceholderToNull(tokens[1]);
        var className = PlaceholderToNull(tokens[2]);
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(className) && controlId < 0)
        {
            return null;
        }

        return new NumadoraElementQueryArgs(
            tokens[0],
            title,
            className,
            controlId < 0 ? null : controlId,
            tokens[4],
            maxDepth,
            maxResults);
    }

    private static NumadoraBrowserSelectorArgs? ParseNumadoraBrowserSelectorArgs(IReadOnlyList<string> args)
    {
        var tokens = SplitNumadoraArgs(args);
        if (tokens.Length < 4
            || !int.TryParse(tokens[2], out var timeoutMs)
            || timeoutMs < 1)
        {
            return null;
        }

        return new NumadoraBrowserSelectorArgs(tokens[0], tokens[1], timeoutMs, tokens[3]);
    }

    private static NumadoraBrowserAttributeArgs? ParseNumadoraBrowserAttributeArgs(IReadOnlyList<string> args)
    {
        var tokens = SplitNumadoraArgs(args);
        if (tokens.Length < 5
            || !int.TryParse(tokens[3], out var timeoutMs)
            || timeoutMs < 1)
        {
            return null;
        }

        return new NumadoraBrowserAttributeArgs(tokens[0], tokens[1], tokens[2], timeoutMs, tokens[4]);
    }

    private static NumadoraLocalHostCallResult NumadoraBrowserInvalidSelectorResult()
    {
        return NumadoraLocalHostCallResult.Failed(
            "numadora_host_call_invalid_arguments",
            "slasher_browser selector calls require using, value, timeoutMs, and sessionId.",
            executedBy: "slasher-browser");
    }

    private static string? ParseOptionalSessionId(IReadOnlyList<string> args)
    {
        var value = string.Join(' ', args).Trim();
        return ParseOptionalSessionId(value);
    }

    private static string? ParseOptionalSessionId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || value.Equals("-", StringComparison.Ordinal)
            ? null
            : value;
    }

    private static object BrowserResult(BrowserSessionResponse session, NumadoraPolicyDecision policyDecision)
    {
        return new
        {
            session.SessionId,
            session.Browser,
            session.Url,
            session.Title,
            executedBy = "slasher-browser",
            policyAllowed = true,
            policyCode = policyDecision.Code
        };
    }

    private static object BrowserValueResult(BrowserValueResponse value, NumadoraPolicyDecision policyDecision)
    {
        return new
        {
            value.Value,
            executedBy = "slasher-browser",
            policyAllowed = true,
            policyCode = policyDecision.Code
        };
    }

    private static bool TryResolveNumadoraElementScope(
        string scope,
        AutomationTarget? target,
        out string? handle,
        out NumadoraLocalHostCallResult error)
    {
        handle = null;
        error = null!;
        if (scope.Equals("foreground", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (scope.Equals("selected", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(target?.Handle))
            {
                handle = target.Handle;
                return true;
            }

            error = NumadoraLocalHostCallResult.Failed(
                "numadora_policy_missing_target",
                "selected element scope requires an observable target handle.",
                executedBy: "slasher-element",
                target: target);
            return false;
        }

        handle = scope;
        return true;
    }

    private static string[] SplitNumadoraArgs(IReadOnlyList<string> args)
    {
        return string.Join(' ', args)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string? PlaceholderToNull(string value)
    {
        return value.Equals("-", StringComparison.Ordinal) ? null : value;
    }

    private sealed record NumadoraLocalHostCallResult(
        bool Ok,
        object? Result,
        AutomationTarget? Target,
        AutomationError? Error,
        string ExecutedBy,
        ScreenshotResponse? Screenshot = null)
    {
        public static NumadoraLocalHostCallResult Passed(
            object? result,
            AutomationTarget? target = null,
            string executedBy = "slasher-host",
            ScreenshotResponse? screenshot = null)
        {
            return new NumadoraLocalHostCallResult(true, result, target, null, executedBy, screenshot);
        }

        public static NumadoraLocalHostCallResult Failed(
            string code,
            string message,
            string executedBy,
            AutomationTarget? target = null,
            object? expected = null,
            object? actual = null)
        {
            return new NumadoraLocalHostCallResult(
                false,
                new { executedBy },
                target,
                new AutomationError(
                    code,
                    message,
                    Action: "numadora.hostCall",
                    Target: target,
                    Recoverable: false,
                    Expected: expected,
                    Actual: actual),
                executedBy,
                null);
        }
    }

    private readonly record struct NumadoraMouseArgs(string Action, int X, int Y, string Button);

    private readonly record struct NumadoraWheelArgs(int X, int Y, int Delta);

    private readonly record struct NumadoraDragArgs(int FromX, int FromY, int ToX, int ToY, string Button, int DurationMs, int Steps);

    private readonly record struct NumadoraContextMenuArgs(int X, int Y, int DelayMs);

    private readonly record struct NumadoraCaptureArgs(string Scope, int MaxWidth, int MaxHeight);

    private readonly record struct NumadoraElementTreeArgs(string Scope, int MaxDepth, int MaxChildren);

    private readonly record struct NumadoraElementQueryArgs(
        string Scope,
        string? Title,
        string? ClassName,
        int? ControlId,
        string Match,
        int MaxDepth,
        int MaxResults);

    private readonly record struct NumadoraBrowserSelectorArgs(string Using, string Value, int TimeoutMs, string SessionId);

    private readonly record struct NumadoraBrowserAttributeArgs(string Using, string Value, string Attribute, int TimeoutMs, string SessionId);
}
