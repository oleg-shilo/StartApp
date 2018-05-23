using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;

namespace System.Windows
{
    public static class Desktop
    {
        public static IntPtr GetFirstWindow(Predicate<IntPtr> filter)
        {
            IntPtr window = Win32.GetWindow(Win32.GetDesktopWindow(), Win32.GW_CHILD);

            while (window != IntPtr.Zero)
            {
                if (filter(window))
                    return window;
                window = Win32.GetWindow(window, Win32.GW_HWNDNEXT);
            }

            return IntPtr.Zero;
        }

        public static IntPtr[] GetWindows(Predicate<IntPtr> filter)
        {
            var result = new List<IntPtr>();
            IntPtr window = Win32.GetWindow(Win32.GetDesktopWindow(), Win32.GW_CHILD);

            while (window != IntPtr.Zero)
            {
                window = Win32.GetWindow(window, Win32.GW_HWNDNEXT);
                if (filter(window))
                    result.Add(window);
            }

            return result.ToArray();
        }

        public static bool IsValid(this IntPtr window) => window != IntPtr.Zero;

        public static bool IsVisible(this IntPtr window) => Win32.IsWindowVisible(window) != 0;

        public static bool IsMinimized(this IntPtr window) => Win32.IsIconic(window);

        public static bool GetRect(this IntPtr window, out Win32.RECT rect) => Win32.GetWindowRect(window, out rect);
        public static int Height(this Win32.RECT rect) => rect.Bottom - rect.Top;

        public static void ShowAndRestore(this IntPtr window)
        {
            if (window.IsValid())
            {
                window.Restore();
                window.Show();
                window.SetForegroundWindow();

                if (window.GetRect(out Win32.RECT rect))
                {
                    if (rect.Height() < 200) 
                    {
                        // collapsed; it happens (e.g. VSCode complex windows restore behavior)
                        var ratio = 0.7;
                        var screen = Screen.FromPoint(new Point(Cursor.Position.X, Cursor.Position.Y)).Bounds;
                        Win32.MoveWindow(window, screen.X, screen.Y, (int)(screen.Width * ratio), (int)(screen.Height * ratio), true);
                    }
                }
            }
        }

        public static void Show(this IntPtr window) => Win32.ShowWindow(window, Win32.SW_SHOW);

        public static void Restore(this IntPtr window) => Win32.ShowWindow(window, Win32.SW_RESTORE);

        public static void Hide(this IntPtr window) => Win32.ShowWindow(window, Win32.SW_HIDE);

        public static void SetForegroundWindow(this IntPtr wnd) => Win32.SetForegroundWindow(wnd);

        public static IntPtr[] GetDesktopWindows()
        {
            var windows = new List<IntPtr>();
            IntPtr hDesktop = IntPtr.Zero; // current desktop
            bool success = Win32.EnumDesktopWindows(
                hDesktop,
                (hWnd, param) =>
                {
                    windows.Add(hWnd);
                    return true;
                },
                IntPtr.Zero);

            return windows.ToArray();
        }

        public static string GetWindowText(IntPtr wnd)
        {
            int length = Win32.GetWindowTextLength(wnd);
            var sb = new StringBuilder(length + 1);
            Win32.GetWindowText(wnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static class Win32
        {
            internal const int SW_HIDE = 0;
            internal const int SW_RESTORE = 9;
            internal const int SW_SHOW = 5;
            internal const int GW_CHILD = 5;
            internal const int GW_HWNDNEXT = 2;

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsIconic(IntPtr hWnd);

            [DllImport("user32.dll")]
            internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            internal static extern int GetWindowTextLength(IntPtr hWnd);

            [DllImport("user32")]
            public static extern IntPtr GetWindow(IntPtr hwnd, int wCmd);

            [DllImport("user32")]
            public static extern int IsWindowVisible(IntPtr hwnd);

            [DllImport("user32")]
            public static extern IntPtr GetDesktopWindow();

            public delegate bool EnumWindowProc(IntPtr hWnd, IntPtr parameter);

            [DllImport("user32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool EnumChildWindows(IntPtr window, EnumWindowProc callback, IntPtr i);

            [DllImport("user32.dll")]
            public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowProc lpfn, IntPtr lParam);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetWindowRect(IntPtr window, out RECT lpRect);

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int Width, int Height, bool Repaint);

        }
    }
}