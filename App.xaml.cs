using System;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using DiscordRPC;
using DiscordRPC.Logging;
using Microsoft.Extensions.Logging;
using Plex.ServerApi.Api;
using Plex.ServerApi.Clients;
using Plex.ServerApi.Clients.Interfaces;
using Plex.ServerApi.PlexModels.Account;
using Plex.ServerApi.PlexModels.OAuth;
using DyviniaUtils;
using DyviniaUtils.Dialogs;
using System.Collections.Generic;

namespace PlexampRPC {

    [GlobalConfig]
    public class Config : SettingsManager<Config> {
        public bool UpdateChecker { get; set; } = true;
        public bool LocalAddress { get; set; } = false;
        public List<String> Skipped { get; set; } = new();
        public bool OwnedOnly { get; set; } = true;
        public bool AllMedia { get; set; } = false;

        public int ArtResolution { get; set; } = 128;
        public double RefreshInterval { get; set; } = 2.5;
        public int SessionTimeout { get; set; } = 30;

        public string TemplateL1 { get; set; } = "{title}";
        public string TemplateL2 { get; set; } = "by {artist}";

        public string DiscordClientID { get; set; } = "1100233636491563069";

        public string PlexAddress { get; set; } = String.Empty;
    }


    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public static readonly string Version = "v" + Assembly.GetExecutingAssembly().GetName()?.Version?.ToString()[..5];

        public static DiscordRpcClient DiscordClient { get; } = new(Config.Settings.DiscordClientID);

        public static IPlexAccountClient AccountClient { get; } = new PlexAccountClient(new() {
            Product = "PlexampRPC",
            DeviceName = Environment.MachineName,
            Platform = "Desktop",
            Version = "v2"
        }, new ApiService(new PlexRequestsHttpClient(), new Logger<ApiService>(LoggerFactory.Create(builder => { builder.AddConsole(); }))));

        public static string? Token { get; set; }
        public static PlexAccount? Account { get; set; }
        public static Resource[]? PlexResources { get; set; }

        public static LogWriter? Log { get; set; }

        public App() {
            Config.Load();

            Log = new LogWriter();

            DiscordInit();

            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => DiscordClient.Dispose();

            DispatcherUnhandledException += ExceptionDialog.UnhandledException;
        }

        protected override async void OnStartup(StartupEventArgs e) {
            MainWindow window = new();
            window.Show();

            await PlexSignIn();

            window.WindowState = WindowState.Normal;
            window.Activate();
            window.GetAccountInfo();
            window.StartPolling();

            foreach (Process existingProcess in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName))
                if (existingProcess.Id != Environment.ProcessId)
                    existingProcess.Kill();

            if (Config.Settings.UpdateChecker)
                await GitHub.CheckAndInstall("Dyvinia", "PlexampRPC");
        }

        protected override void OnExit(ExitEventArgs e) {
            DiscordClient.Dispose();
        }

        private static void DiscordInit() {
            DiscordClient.Logger = new ConsoleLogger() { Level = DiscordRPC.Logging.LogLevel.Warning };

            DiscordClient.OnReady += (_, e) => Console.WriteLine($"Connected to {e.User.Username}'s Discord Client");

            DiscordClient.OnPresenceUpdate += (_, e) => {
                if (e.Presence != null)
                    Console.WriteLine($"Updated Presence to [{e.Presence?.Details} | {e.Presence?.State}]");
                else
                    Console.WriteLine($"Cleared Presence");
            };

            DiscordClient.Initialize();
        }

        private static async Task PlexSignIn(bool resignIn = false) {
            string authFile = Path.Combine(Path.GetDirectoryName(Config.FilePath)!, "auth.token");
            if (File.Exists(authFile) && !resignIn) {
                Token = File.ReadAllText(authFile);
            }
            else {
                Token = await PlexOAuth();
                File.WriteAllText(authFile, Token);
            }

            try {
                Account = await AccountClient.GetPlexAccountAsync(Token);
                PlexResources = await Resource.GetAccountResources();
            }
            catch {
                _ = PlexSignIn(true);
            }
        }

        private static async Task<string> PlexOAuth() {
            OAuthPin plexPin;
            OAuthPin? oauthUrl = await AccountClient.CreateOAuthPinAsync("");

            Process.Start(new ProcessStartInfo(oauthUrl.Url) { UseShellExecute = true });

            while (true) {
                plexPin = await AccountClient.GetAuthTokenFromOAuthPinAsync(oauthUrl.Id.ToString());
                if (!String.IsNullOrEmpty(plexPin.AuthToken)) break;
                await Task.Delay(1000);
            }
            return plexPin.AuthToken;
        }
    }
}
