using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

static class Extensions
{
    public static string JoinBy(this IEnumerable<string> items, string separator) => string.Join(separator, items.ToArray());

    public static void StartWith(this string exe, string args)
    {
        try { Process.Start(exe, args); } catch { };
    }

    public static void WriteToFile(this string content, string path) => File.WriteAllText(path, content);

    public static string PathJoin(this string path, string item) => Path.Combine(path, item);

    public static string GetDirName(this string path) => Path.GetDirectoryName(path);

    public static string GetFileName(this string path) => Path.GetFileName(path);

    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> collection, Action<T> action)
    {
        foreach (T item in collection)
            action(item);
        return collection;
    }
}

public class AppInfo
{
    static DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppInfo));

    public static AppInfo FromJson(string json)
    {
        try
        {
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return serializer.ReadObject(ms) as AppInfo;
        }
        catch { }
        return null;
    }

    public string ToJson()
    {
        var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true };

        using (var ms = new MemoryStream())
        {
            using (var writer = XmlWriter.Create(ms, settings))
            {
                serializer.WriteObject(writer, this);
            }

            byte[] data = ms.ToArray();
            return Encoding.UTF8.GetString(data, 0, data.Length);
        }
    }

    // The apps may set themselves visible at startup multiple times so they
    // should be set 'hidden' multiple times during `HidingDuration` period
    public int StartWhenNoneFound_HidingDuration;

    public string Name;
    public string File;
    public string StartWhenNoneFound_Args;
    public bool Enabled = true;
    public bool StartWhenNoneFound = false;

    internal Action OnCaptured;
    public string WndFilter { get => filter_pattern; set { filter_pattern = value; filter = new Regex(value); } }

    string filter_pattern;

    // ----------------------

    IntPtr preloadedInstance;

    internal IntPtr PreloadedInstance
    {
        get { return preloadedInstance; }

        set { preloadedInstance = value; OnCaptured?.Invoke(); }
    }

    internal int IdleCount = 0;
    internal string ConfigName;

    internal bool WindowFilter(IntPtr handle)
    {
        string wnd_text = Desktop.GetWindowText(handle);
        var match = filter.Match(wnd_text);
        return filter.Match(wnd_text).Success;
    }

    Regex filter;

    public static AppInfo Notepad =
        new AppInfo
        {
            Name = "Notepad",
            File = "Notepad.exe",
            StartWhenNoneFound_Args = "",
            Enabled = true,
            WndFilter = " - Notepad$",
            StartWhenNoneFound_HidingDuration = 500
        };
}

class AppLauncher
{
    public static void HideAbandonedMinimized(AppInfo app)
    {
        if (app.Enabled)
        {
            var all = Desktop.GetWindows(app.WindowFilter);

            var minimized = all.Where(x => x.IsMinimized());

            if (minimized.Count() == 1 && all.Count() == 1)
                if (minimized.First().IsVisible())
                    minimized.First().Hide();
        }
    }

    static bool checkingInProgress = false;

    public static void StartCheckForHotInstance(AppInfo[] apps)
    {
        lock (typeof(AppLauncher))
        {
            if (!checkingInProgress)
            {
                checkingInProgress = true;
                Task.Run(() =>
                {
                    try
                    {
                        apps.ForEach(app =>
                            AppLauncher.CheckForHotInstance(app));

                    }
                    catch { }
                    finally { checkingInProgress = false; }
                });
            }
        }
    }

    public static void CheckForHotInstance(AppInfo app)
    {
        if (app.Enabled)
        {
            HideAbandonedMinimized(app);
            FindHotInstance(app);

            if (app.StartWhenNoneFound)
            {
                if (!app.PreloadedInstance.IsValid())
                    app.IdleCount++;
                else
                    app.IdleCount = 0;

                if (app.IdleCount > 1)
                    EnsureHotInstance(app);
            }
        }
    }

    public static void Run(string[] args, AppInfo[] apps)
    {
        var requested_config = args.First() + ".json";
        var app = apps.FirstOrDefault(x => x.ConfigName == requested_config);

        if (app == null)
            return;

        var vs_code = AppLauncher.FindHotInstance(app);

        bool already_active = false;
        if (vs_code.IsValid())
        {
            vs_code.ShowAndRestore();
            already_active = true;
        }

        if (!already_active || args.Length > 1)
        {
            var exe = app.File;
            var exe_args = "";
            if (args.Length > 1)
                exe_args = $"\"{args.Skip(1).JoinBy("\" \"")}\"";
            else
                exe_args = app.StartWhenNoneFound_Args;
            Process.Start(exe, exe_args);
        }
    }

    static void StartAndHide(AppInfo app)
    {
        app.PreloadedInstance = IntPtr.Zero;

        Process.Start(app.File, app.StartWhenNoneFound_Args).WaitForInputIdle();

        int start = Environment.TickCount;
        int timeout = 10000;

        while (!app.PreloadedInstance.IsValid() && (Environment.TickCount - start) < timeout)
            app.PreloadedInstance = Desktop.GetFirstWindow(app.WindowFilter);

        if (app.PreloadedInstance.IsValid())
        {
            app.PreloadedInstance.Hide();

            int delay = 50;
            for (int i = 0; i < app.StartWhenNoneFound_HidingDuration / delay; i++)
            {
                Thread.Sleep(delay);
                app.PreloadedInstance.Hide();
            }
        }
    }

    static void EnsureHotInstance(AppInfo app)
    {
        var app_window = Desktop.GetFirstWindow(app.WindowFilter);

        if (!app_window.IsValid())
            StartAndHide(app);
    }

    public static IntPtr FindHotInstance(AppInfo app)
    {
        var app_window = Desktop.GetFirstWindow(wnd =>
            wnd.IsValid() && !wnd.IsVisible() && app.WindowFilter(wnd));

        app.PreloadedInstance = app_window;

        app.OnCaptured?.Invoke();
        return app.PreloadedInstance;
    }
}