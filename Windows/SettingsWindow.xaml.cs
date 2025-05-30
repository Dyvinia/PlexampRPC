using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace PlexampRPC {
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            Title += $" {App.Version}";

            CheckForStartup();

            // Handle "Start on Startup" checkbox
            StartupCheckBox.Checked += (_, _) =>
            {
                CreateStartupShortcut();
                TrayStartupCheckBox.IsEnabled = true;
            };

            StartupCheckBox.Unchecked += (_, _) =>
            {
                string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "PlexampRPC.lnk");
                if (File.Exists(shortcutPath))
                    File.Delete(shortcutPath);

                TrayStartupCheckBox.IsEnabled = false;
                TrayStartupCheckBox.IsChecked = false;
            };

            // Handle "Start in Tray" checkbox changes
            TrayStartupCheckBox.Checked += (_, _) =>
            {
                if (StartupCheckBox.IsChecked == true)
                    CreateStartupShortcut();
            };

            TrayStartupCheckBox.Unchecked += (_, _) =>
            {
                if (StartupCheckBox.IsChecked == true)
                    CreateStartupShortcut();
            };

            // Set initial enabled/checked state
            TrayStartupCheckBox.IsEnabled = StartupCheckBox.IsChecked == true;
            if (StartupCheckBox.IsChecked != true)
                TrayStartupCheckBox.IsChecked = false;

            SetupListeningTo();

            MouseDown += (_, _) => FocusManager.SetFocusedElement(this, this);

            DataContext = Config.Settings;
        }


        private void SetupListeningTo() {
            RadioListeningPlexamp.Checked += (_, _) => Config.Settings.DiscordListeningTo = "Plexamp";
            RadioListeningMusic.Checked += (_, _) => Config.Settings.DiscordListeningTo = "Music";
            RadioListeningCustom.Checked += (_, _) => Config.Settings.DiscordListeningTo = "Custom";

            if (Config.Settings.DiscordListeningTo == "Plexamp")
                RadioListeningPlexamp.IsChecked = true;
            else if (Config.Settings.DiscordListeningTo == "Music")
                RadioListeningMusic.IsChecked = true;
            else
                RadioListeningCustom.IsChecked = true;
        }

        private void CreateStartupShortcut()
        {
            string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "PlexampRPC.lnk");

            IWshRuntimeLibrary.WshShell wshShell = new();
            IWshRuntimeLibrary.IWshShortcut shortcut = wshShell.CreateShortcut(shortcutPath);

            shortcut.TargetPath = Environment.ProcessPath;
            shortcut.WorkingDirectory = Environment.CurrentDirectory;

            // Add "--tray" if the tray option is enabled
            if (TrayStartupCheckBox.IsChecked == true)
                shortcut.Arguments = "--tray";
            else
                shortcut.Arguments = "";

            shortcut.Save();
        }

        private void CheckForStartup()
        {
            string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "PlexampRPC.lnk");

            if (File.Exists(shortcutPath))
            {
                StartupCheckBox.IsChecked = true;

                // Read the shortcut and check if it includes the "--tray" argument
                IWshRuntimeLibrary.WshShell shell = new();
                IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);

                if (shortcut.Arguments?.Contains("--tray") == true)
                    TrayStartupCheckBox.IsChecked = true;
                else
                    TrayStartupCheckBox.IsChecked = false;
            }
            else
            {
                StartupCheckBox.IsChecked = false;
                TrayStartupCheckBox.IsChecked = false;
            }

            TrayStartupCheckBox.IsEnabled = StartupCheckBox.IsChecked == true;
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            if (e.Key == Key.F12)
                Process.Start("explorer.exe", $"/select, {Config.FilePath}");
        }
    }
}
