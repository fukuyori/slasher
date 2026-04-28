using System.Runtime.InteropServices;
using System.Text;

namespace Slasher.Windows;

internal static partial class NativeMethods
{
    internal const int SwHide = 0;
    internal const int SwShow = 5;
    internal const int SwMinimize = 6;
    internal const int SwRestore = 9;
    internal const int SwMaximize = 3;

    internal const uint WmClose = 0x0010;
    internal const uint InputKeyboard = 1;
    internal const uint InputMouse = 0;
    internal const uint KeyEventFKeyUp = 0x0002;
    internal const uint KeyEventFUnicode = 0x0004;
    internal const uint MouseEventFMove = 0x0001;
    internal const uint MouseEventFLeftDown = 0x0002;
    internal const uint MouseEventFLeftUp = 0x0004;
    internal const uint MouseEventFRightDown = 0x0008;
    internal const uint MouseEventFRightUp = 0x0010;
    internal const uint MouseEventFMiddleDown = 0x0020;
    internal const uint MouseEventFMiddleUp = 0x0040;
    internal const uint MouseEventFWheel = 0x0800;
    internal const int Srccopy = 0x00CC0020;
    internal const int StretchHalftone = 4;
    internal const uint DibRgbColors = 0;
    internal const int SmXVirtualScreen = 76;
    internal const int SmYVirtualScreen = 77;
    internal const int SmCxVirtualScreen = 78;
    internal const int SmCyVirtualScreen = 79;
}

