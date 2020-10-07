using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using static NvidiaHijackSharp.Enums;
using static NvidiaHijackSharp.Structs;
using static NvidiaHijackSharp.Enums.SetWindowPosFlags;
using static NvidiaHijackSharp.Enums.WindowLongFlags;
using static NvidiaHijackSharp.Enums.WindowStyles;
using static NvidiaHijackSharp.Enums.GetWindowType;


namespace NvidiaHijackSharp
{
    class Program
    {
        static IntPtr OverlayWindow;
        static RECT OverlayRect;
        static SharpDX.Direct2D1.Factory d2dFactory;
        static SharpDX.Direct2D1.WindowRenderTarget target;
        static SharpDX.DirectWrite.Factory writeFactory;
        static TextFormat format;
        static Dictionary<string, SolidColorBrush> brushes;
        static bool running = true;

        static IntPtr TargetWindow;

        static WindowStyles nv_default = WS_POPUP | WS_CLIPSIBLINGS;
        static WindowStyles nv_default_in_game = nv_default | WS_DISABLED;
        static WindowStyles nv_edit = nv_default_in_game | WS_VISIBLE;

        static WindowStyles nv_ex_default = WS_EX_TOOLWINDOW;
        static WindowStyles nv_ex_edit = nv_ex_default | WS_EX_LAYERED | WS_EX_TRANSPARENT;
        static WindowStyles nv_ex_edit_menu = nv_ex_default | WS_EX_TRANSPARENT;

        static void Main(string[] args)
        {
            if (!InitialiseWindow())
            {
                Console.WriteLine("Failed to initialise Nvidia overlay window");
                return;
            }

            if (!InitialiseD2D())
            {
                Console.WriteLine("Failed to initialise D2D target");
                return;
            }

            TargetWindow = FindWindowAlt("UnityWndClass", "FallGuys_client");
            TargetWindow = FindWindowAlt("WinListerMain", "WinLister");

            if (TargetWindow == IntPtr.Zero)
            {
                Console.WriteLine("Running without game HWND");
            }

            Console.WriteLine("Window: " + OverlayWindow.ToString("X"));
            Console.WriteLine("D2D Target: " + target);
            Console.WriteLine("Starting render thread");
            Console.WriteLine("Input anything and press [ENTER] to exit.");

            var thread = new Thread(Render);
            thread.Start();

            Console.ReadLine();
            running = false;


            thread.Join();
            BeginScene();
            ClearScene();
            EndScene();

            Dispose();
            Console.WriteLine("See you next time.");
        }

        static void Render()
        {
            while (running)
            {
                BeginScene();
                ClearScene();

                RawRectangleF rectF;
                if (TargetWindow == IntPtr.Zero)
                {
                    rectF = new RawRectangleF(50, 50, OverlayRect.Right, OverlayRect.Bottom);
                }
                else
                {
                    GetWindowRect(TargetWindow, out RECT TargetRect);
                    //rectF = new RawRectangleF(50, 50, OverlayRect.Right, OverlayRect.Bottom);
                    rectF = new RawRectangleF(50 + TargetRect.Left, 50 + TargetRect.Top, OverlayRect.Right, OverlayRect.Bottom);
                    WindowSetAbove();

                    SetWindowLongPtr(OverlayWindow, (int)GWL_STYLE, new IntPtr((long)nv_edit));
                    SetWindowLongPtr(OverlayWindow, (int)GWL_EXSTYLE, new IntPtr((long)nv_ex_edit));

                }

                target.DrawText("Hello my name is Zhengyu Wu", format, rectF, brushes["red"]);

                EndScene();

                Thread.Sleep(1000 / 144);
            }

            Console.WriteLine("End of render thread");
        }

        static void BeginScene()
        {
            target.BeginDraw();
        }

        static void EndScene()
        {
            target.EndDraw();
        }

        static void ClearScene()
        {
            target.Clear(new RawColor4(1, 1, 1, 0));
        }

        static void Dispose()
        {

            target.Dispose();
            d2dFactory.Dispose();
            writeFactory.Dispose();
            foreach (var pair in brushes) pair.Value.Dispose();
        }

        static bool InitialiseWindow()
        {
            OverlayWindow = FindWindow("CEF-OSC-WIDGET", "NVIDIA GeForce Overlay");

            if (OverlayWindow == IntPtr.Zero)
            {
                return false;
            }

            WindowSetStyle();
            WindowSetTransparency();
            //WindowSetTopmost();

            ShowWindow(OverlayWindow, (int)ShowWindowCommands.SW_SHOW);

            return true;
        }

        static void WindowSetStyle()
        {
            long i = (long)GetWindowLongPtr(OverlayWindow, (int)GWL_EXSTYLE);
            i |= (long)WS_EX_TRANSPARENT;
            SetWindowLongPtr(OverlayWindow, (int)GWL_EXSTYLE, (IntPtr)i);
        }

        static void WindowSetTransparency()
        {
            MARGINS margin = new MARGINS()
            {
                bottomHeight = -1,
                leftWidth = -1,
                rightWidth = -1,
                topHeight = -1
            };

            DwmExtendFrameIntoClientArea(OverlayWindow, ref margin);

            SetLayeredWindowAttributes(OverlayWindow, 0x000000, 0xFF, 0x02); //LWA_ALPHA = 0x2; LWA_COLORKEY = 0x1;
        }

        static bool WindowSetAbove()
        {
            IntPtr Window = GetWindow(TargetWindow, GW_HWNDPREV);
            return SetWindowPos(OverlayWindow, Window, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_ASYNCWINDOWPOS);
            //return SetWindowPos(TargetWindow, OverlayWindow, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_ASYNCWINDOWPOS);
        }

        static bool InitialiseD2D()
        {
            d2dFactory = new SharpDX.Direct2D1.Factory(SharpDX.Direct2D1.FactoryType.SingleThreaded);
            writeFactory = new SharpDX.DirectWrite.Factory(SharpDX.DirectWrite.FactoryType.Shared);

            GetClientRect(OverlayWindow, out OverlayRect);

            var renderProps = new RenderTargetProperties
            {
                Type = RenderTargetType.Default,
                PixelFormat = new PixelFormat(Format.Unknown, SharpDX.Direct2D1.AlphaMode.Premultiplied),
                Usage = RenderTargetUsage.None
            };

            var hwndProps = new HwndRenderTargetProperties()
            {
                Hwnd = OverlayWindow,
                PixelSize = new Size2(OverlayRect.Width, OverlayRect.Height),
                PresentOptions = PresentOptions.Immediately //Replace for None
            };

            target = new WindowRenderTarget(d2dFactory, renderProps, hwndProps)
            {
                AntialiasMode = AntialiasMode.PerPrimitive
            };

            format = new TextFormat(writeFactory, "Segoe Ui Light", 16);

            brushes = new Dictionary<string, SolidColorBrush>()
            {
                {"red", new SolidColorBrush(target, new RawColor4(1, 0, 0, 1)) },
                {"green", new SolidColorBrush(target, new RawColor4(0, 1, 0, 1)) },
                {"blue", new SolidColorBrush(target, new RawColor4(0, 0, 1, 1)) },
                {"black", new SolidColorBrush(target, new RawColor4(0, 0, 0, 1)) },
                {"white", new SolidColorBrush(target, new RawColor4(1, 1, 1, 1)) }
            };

            return target != null;
        }


        //Native API
        [DllImport("User32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLongPtr32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLongPtr32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            else
                return GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("dwmapi.dll")]
        static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [DllImport("user32.dll")]
        static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, Enums.SetWindowPosFlags uFlags);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        public static string GetClassName(IntPtr hWnd)
        {
            int size = 256;
            if (size > 0)
            {
                var builder = new StringBuilder(size + 1);
                GetClassName(hWnd, builder, builder.Capacity);
                return builder.ToString();
            }

            return String.Empty;
        }

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hwnd, StringBuilder lptrString, int nMaxCount);

        public static string GetWindowText(IntPtr hWnd)
        {
            int size = GetWindowTextLength(hWnd);
            if (size > 0)
            {
                var builder = new StringBuilder(size + 1);
                GetWindowText(hWnd, builder, builder.Capacity);
                return builder.ToString();
            }

            return String.Empty;
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        public static IEnumerable<IntPtr> FindWindows(EnumWindowsProc filter)
        {
            IntPtr found = IntPtr.Zero;
            List<IntPtr> Windows = new List<IntPtr>();

            EnumWindows(delegate (IntPtr wnd, IntPtr param)
            {
                if (filter(wnd, param))
                {
                    Windows.Add(wnd);
                }
                return true;
            }, IntPtr.Zero);

            return Windows;
        }

        public static IntPtr FindWindowAlt(string ClassName, string WindowName)
        {
            IEnumerable<IntPtr> Windows = FindWindows(delegate (IntPtr wnd, IntPtr param)
            {
                return GetWindowText(wnd).Contains(WindowName) && GetClassName(wnd).Contains(ClassName);
            });

            return Windows.DefaultIfEmpty(IntPtr.Zero).First();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, GetWindowType uCmd);
    }
}
