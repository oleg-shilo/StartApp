using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace VSCode
{
    public partial class AppMonitor : Form
    {
        public static void Start()
        {
            new AppMonitor().ShowDialog();
        }

        public AppMonitor()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var vs_code = AppStart.FindHotInstance(AppInfo.VSCode);

            if (vs_code.IsValid())
                vs_code.Show();

            var file = @"E:\My Utils\IuSpy\bin\Debug\IuSpy.exe.config";
            var proc = Process.Start(@"C:\Program Files (x86)\Microsoft VS Code\Code.exe", $"--wait -r \"{file}\"");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            AppInfo.VSCode.PreloadedInstance.Hide();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            AppInfo.VSCode.PreloadedInstance.Show();
        }

        void timer1_Tick(object sender, EventArgs e)
        {
            AppStart.CheckForHotInstance(AppInfo.VSCode);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void hideToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void showToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void AppMonitor_FormClosing(object sender, FormClosingEventArgs e)
        {
            notifyIcon1.Visible = false;
        }
    }
}