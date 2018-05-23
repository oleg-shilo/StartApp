using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;

namespace VSCode
{
    class Program
    {
        static void Main(string[] args)
        {
            // Debug.Assert(false);

            if (args.Contains("-new"))
            {
                AppMonitor.New();
            }
            else
            {
                AppInfo[] apps = ReadConfig();

                var m = new System.Threading.Mutex(true, "AppStart_Instance", out bool createdNew);

                if (args.Any())
                {
                    AppLauncher.Run(args, apps);
                    if (createdNew) // the only running instance of AppStart
                        AppMonitor.Run(apps);
                }
                else
                {
                    AppMonitor.Run(apps);
                }
            }
        }

        static AppInfo[] ReadConfig()
        {
            return Directory.GetFiles(Assembly.GetExecutingAssembly().Location.GetDirName(), "*.json")
                            .Select(configFile =>
                            {
                                var info = AppInfo.FromJson(File.ReadAllText(configFile));
                                info.ConfigName = configFile.GetFileName();
                                if (info.Name == null)
                                    info.Name = info.ConfigName;
                                return info;
                            })
                            .Where(x => x?.Name != null)
                            .ToArray();
        }
    }
}

partial class AppMonitor
{
    internal static bool notifyIconMenuVisible = false;
    public static void New()
    {
        var new_config = Assembly.GetExecutingAssembly().Location.GetDirName().PathJoin("notepad.json");

        AppInfo.Notepad.ToJson().WriteToFile(new_config);

        "Notepad.exe".StartWith(new_config);
    }

    public static void Run(AppInfo[] apps)
    {
        bool showAllOnExit = true;

        var notifyIcon = new NotifyIcon();
        var menuStrip = new ContextMenuStrip();

        var configMenu = new ToolStripMenuItem("Config", null, (s, e) => Process.Start(Assembly.GetExecutingAssembly().Location.GetDirName()));
        var showAllMenu = new ToolStripMenuItem("Show All", null, (s, e) => apps.ForEach(app => app.PreloadedInstance.ShowAndRestore()));
        var newMenu = new ToolStripMenuItem("<New>", null, (s, e) => AppMonitor.New());
        var restartMenu = new ToolStripMenuItem("Restart", null, (s, e) => { Application.Restart(); showAllOnExit = false; });
        var exitMenu = new ToolStripMenuItem("Exit", null, (s, e) => Application.Exit());

        bool captured = false;
        void update_tray_icon()
        {
            bool new_captured = apps.Any(x => x.PreloadedInstance.IsValid());
            if (new_captured != captured)
                notifyIcon.Icon = new_captured ? StartApp.Properties.Resources.appActive : StartApp.Properties.Resources.app_icon;
            captured = new_captured;
        }

        notifyIcon.ContextMenuStrip = menuStrip;
        notifyIcon.Icon = StartApp.Properties.Resources.app_icon;
        notifyIcon.Text = "App instant start";
        notifyIcon.DoubleClick += (s, e) => apps.ForEach(app => app.PreloadedInstance.ShowAndRestore());
        notifyIcon.ContextMenuStrip.Opening += (s, e) => notifyIconMenuVisible = true;
        notifyIcon.ContextMenuStrip.Closed += (s, e) => notifyIconMenuVisible = false;

        menuStrip.Items.Add(configMenu);
        menuStrip.Items.Add(newMenu);
        menuStrip.Items.Add("-");
        foreach (var app in apps)
        {
            var appMenu = new ToolStripMenuItem(app.Name);
            appMenu.Enabled = app.Enabled;
            appMenu.Click += (s, e) =>
            {
                AppLauncher.FindHotInstance(app); //just in case if it was not found before
                if (app.PreloadedInstance.IsValid())
                    app.PreloadedInstance.ShowAndRestore();
                else
                    "Notepad.exe".StartWith(Assembly.GetExecutingAssembly().Location.GetDirName().PathJoin(app.ConfigName));
            };

            menuStrip.Items.Add(appMenu);

            app.OnCaptured = () =>
            {
                if (app.PreloadedInstance.IsValid())
                    appMenu.Text = "> " + app.Name;
                else
                    appMenu.Text = app.Name;

                lock (typeof(AppLauncher))
                    update_tray_icon();
            };
        }

        menuStrip.Items.Add("-");
        menuStrip.Items.Add(showAllMenu);
        menuStrip.Items.Add("-");
        menuStrip.Items.Add(restartMenu);
        menuStrip.Items.Add("-");
        menuStrip.Items.Add(exitMenu);

        notifyIcon.Visible = true;

        var timer = new Timer();
        timer.Interval = 5000;
        timer.Tick += (s, e) =>
        {
            if (!notifyIconMenuVisible)
                AppLauncher.StartCheckForHotInstance(apps);

        };
        timer.Enabled = true;

        Application.Run();

        timer.Enabled = false;
        notifyIcon.Visible = false;

        if (showAllOnExit)
            apps.Where(app => app.Enabled)
                .ForEach(app => app.PreloadedInstance.ShowAndRestore());
    }
}