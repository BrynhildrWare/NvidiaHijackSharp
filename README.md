# NvidiaHijackSharp
A PoC project showing you how to hijack Nvidia Overlay and stay on the top of your target window without TOPMOST

## Key Points
* Get rid or `FindWindow` using `EnumWindows` with custom filter
* Imitate `WS_EX_TOPMOST` using `GetWindow(TargetWindow, GW_HWNDPREV)` and `SetWindowPos(OverlayWindow, Window, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_ASYNCWINDOWPOS)`

## Third-party Library
* SharpDX [https://github.com/sharpdx/SharpDX]
