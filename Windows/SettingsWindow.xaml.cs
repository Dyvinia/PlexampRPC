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

            SetupListeningTo();
            SetupStatusDisplayType();

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

        private void SetupStatusDisplayType() {
            RadioStatusName.Checked += (_, _) => Config.Settings.StatusDisplayType = "Name";
            RadioStatusState.Checked += (_, _) => Config.Settings.StatusDisplayType = "State";
            RadioStatusDetails.Checked += (_, _) => Config.Settings.StatusDisplayType = "Details";

            switch (Config.Settings.StatusDisplayType) {
                case "State":
                    RadioStatusState.IsChecked = true; break;
                case "Details":
                    RadioStatusDetails.IsChecked = true; break;
                default:
                    RadioStatusName.IsChecked = true; break;
            }
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
