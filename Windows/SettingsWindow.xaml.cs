using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace PlexampRPC {
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window {
        public SettingsWindow() {
            InitializeComponent();

            Title += $" {App.Version}";

            CheckForStartup();
            StartupCheckBox.Checked += (_, _) => StartOnStartup();
            StartupCheckBox.Unchecked += (_, _) => File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "PlexampRPC.lnk"));

            MouseDown += (_, _) => FocusManager.SetFocusedElement(this, this);

            DataContext = Config.Settings;
        }

        private void StartOnStartup() {
            IWshRuntimeLibrary.WshShell wshShell = new();
            IWshRuntimeLibrary.IWshShortcut shortcut = wshShell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "PlexampRPC.lnk"));
            shortcut.TargetPath = Environment.ProcessPath;
            shortcut.WorkingDirectory = Environment.CurrentDirectory;
            shortcut.Save();
        }

        private void CheckForStartup() {
            if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "PlexampRPC.lnk"))) {
                StartupCheckBox.IsChecked = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            if (e.Key == Key.F12)
                Process.Start("explorer.exe", $"/select, {Config.FilePath}");
        }
    }
}
