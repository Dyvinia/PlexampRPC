using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DiscordRPC;
using DiscordRPC.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Plex.ServerApi.Api;
using Plex.ServerApi.Clients;
using Plex.ServerApi.Clients.Interfaces;
using Plex.ServerApi.PlexModels.Account;
using Plex.ServerApi.PlexModels.OAuth;

namespace PlexampRPC {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public static IPlexAccountClient AccountClient = new PlexAccountClient(new() {
            Product = "PlexampRPC",
            DeviceName = Environment.MachineName,
            Platform = "Desktop",
            Version = "v2"
        }, new ApiService(new PlexRequestsHttpClient(), new Logger<ApiService>(new NullLoggerFactory())));

        public static IPlexServerClient ServerClient = new PlexServerClient(new() {
            Product = "PlexampRPC",
            DeviceName = Environment.MachineName,
            Platform = "Desktop",
            Version = "v2"
        }, new ApiService(new PlexRequestsHttpClient(), new Logger<ApiService>(new NullLoggerFactory())));


        public static string Token { get; set; }
        public static PlexAccount? Account { get; set; }
        public static AccountServerContainer? ServerContainer { get; set; }
        public static DiscordRpcClient DiscordClient { get; set; }

        public App() {

            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            DispatcherUnhandledException += Application_DispatcherUnhandledException;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => DiscordClient.Dispose();
        }

        protected override async void OnStartup(StartupEventArgs e) {
            MainWindow window = new();
            window.Show();

            InitRPC();
            await PlexSignIn();

            window.WindowState = WindowState.Normal;
            window.Activate();
            window.GetAccountInfo();
            window.StartPolling();
        }

        private async Task PlexSignIn(bool resignIn = false) {
            string authFile = "auth.txt";
            if (File.Exists(authFile) && !resignIn) {
                Token = File.ReadAllText(authFile);
            }
            else {
                Token = await PlexOAuth();
                File.WriteAllText(authFile, Token);
            }
            Account = await AccountClient.GetPlexAccountAsync(Token);
            ServerContainer = await AccountClient.GetAccountServersAsync(Token);
        }

        private async Task<string> PlexOAuth() {
            OAuthPin plexPin;
            OAuthPin? oauthUrl = await AccountClient.CreateOAuthPinAsync("");

            Process.Start(new ProcessStartInfo(oauthUrl.Url) { UseShellExecute = true });

            while (true) {
                plexPin = await AccountClient.GetAuthTokenFromOAuthPinAsync(oauthUrl.Id.ToString());
                if (!string.IsNullOrEmpty(plexPin.AuthToken)) break;
                await Task.Delay(1000);
            }
            return plexPin.AuthToken;
        }

        private void InitRPC() {
            DiscordClient = new("1100233636491563069") {
                Logger = new ConsoleLogger() { Level = DiscordRPC.Logging.LogLevel.Warning }
            };

            DiscordClient.OnReady += (sender, e) => {
                Console.WriteLine($"Received Ready from user {e.User.Username}");
            };

            DiscordClient.OnPresenceUpdate += (sender, e) => {
                Console.WriteLine($"Received Update! {e.Presence}");
            };
            DiscordClient.Initialize();
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
            e.Handled = true;
            string title = "";
            //ExceptionDialog.Show(e.Exception, title, true);
        }
    }
}
