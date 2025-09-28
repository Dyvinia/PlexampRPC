using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using PlexampRPC.Utils;

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
            SetupStatusDisplayTypeNames();

            MouseDown += (_, _) => FocusManager.SetFocusedElement(this, this);

            DataContext = Config.Settings;
        }

        private void SetupListeningTo() {
            RadioListeningPlexamp.Checked += (_, _) => Config.Settings.DiscordListeningTo = "Plexamp";
            RadioListeningMusic.Checked += (_, _) => Config.Settings.DiscordListeningTo = "Music";
            RadioListeningCustom.Checked += (_, _) => Config.Settings.DiscordListeningTo = "Custom";

            switch (Config.Settings.DiscordListeningTo) {
                case "Plexamp":
                    RadioListeningPlexamp.IsChecked = true; break;
                case "Music":
                    RadioListeningMusic.IsChecked = true; break;
                default:
                    RadioListeningCustom.IsChecked = true; break;
            }
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

        private void SetupStatusDisplayTypeNames() {
            RadioStatusDetails.Content = Config.Settings.TemplateL1.ApplyPlaceholders();
            RadioStatusState.Content = Config.Settings.TemplateL2.ApplyPlaceholders();
        }

        private void Template_LostFocus(object sender, RoutedEventArgs e) => SetupStatusDisplayTypeNames();

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
