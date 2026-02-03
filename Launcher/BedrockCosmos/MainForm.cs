using BedrockCosmos.App;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.Examples.Basic;
using Titanium.Web.Proxy.Examples.Basic.Helpers;
using Titanium.Web.Proxy.Helpers;

namespace BedrockCosmos
{
    public partial class MainForm : Form
    {
        private LaunchManager launchManager = new LaunchManager();
        private static ProxyController controller = new ProxyController();
        private readonly AsyncFileDownload asyncDownload;

        // For window movement
        bool drag = false;
        Point start_point = new Point(0, 0);

        public MainForm()
        {
            InitializeComponent();
            CosmosConsole.Initialize(DevConsole);
            asyncDownload = new AsyncFileDownload();
            StatusLabel.Text = "";

            // Will also log messages to the main console if uncommented.
            //CosmosConsole.LogToMainConsole = true;

            launchManager.InitializeMgrAsyncFileDownload(asyncDownload);
            launchManager.InitializeMgrLaunchButton(LaunchButton);
            launchManager.InitializeMgrVersionLabel(VersionLabel);
            launchManager.SetCurrentVersions();

            SettingsManager.LoadSettings();
            ApplySettings();

            if (RunTime.IsWindows)
                // Fix console hang due to QuickEdit mode
                ConsoleHelper.DisableQuickEditMode();
        }

        private void ApplySettings()
        {
            EnableLoggingSwitch.Checked = SettingsManager.EnableLogging;
            BackgroundModeSwitch.Checked = SettingsManager.BackgroundMode;

            if (SettingsManager.DevMenuEnabled)
            {
                SettingsManager.DevMenuClicks = 7;
                AppIcon.Cursor = Cursors.Hand;
                CosmosConsole.WriteLine("Developer mode enabled.");
            }
        }

        private void MinimizeButton_Click(object sender, EventArgs e)
        {
            if (!SettingsManager.BackgroundMode)
            {
                this.WindowState = FormWindowState.Minimized;
            }
            else
            {
                Hide();
                TrayIcon.Visible = true;
            }
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (SettingsManager.ProxyStarted)
            {
                try
                {
                    controller.Stop();
                    controller.Dispose();
                    controller = null;
                }
                catch
                {

                }
            }

            asyncDownload.Dispose();
        }

        private void TrayIcon_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            TrayIcon.Visible = false;
        }

        private void TopPanel_MouseDown(object sender, MouseEventArgs e)
        {
            drag = true;
            start_point = new Point(e.X, e.Y);
        }

        private void TopPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (drag)
            {
                Point p = PointToScreen(e.Location);
                this.Location = new Point(p.X - start_point.X, p.Y - start_point.Y);
            }
        }

        private void TopPanel_MouseUp(object sender, MouseEventArgs e)
        {
            drag = false;
        }

        private async void LaunchButton_Click(object sender, EventArgs e)
        {
            if (!SettingsManager.ProxyStarted)
            {
                LaunchButton.Enabled = false;
                StatusLabel.Text = "";

                CosmosConsole.WriteLine("Starting proxy...");
                LaunchButton.Text = "Entering The Cosmos...";
                launchManager.UpdateLaunchButtonColor("purple");
                await launchManager.InternetCheck();

                if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + @"Misc\CurrentVersion.json"))
                {
                    launchManager.SetLatestVersions();
                    bool updateLauncher = launchManager.CheckLauncherUpdate();

                    if (updateLauncher && !SettingsManager.LauncherUpdatePrompted)
                    {
                        SettingsManager.LauncherUpdatePrompted = true;
                        TabControl.SelectedTab = UpdatePage;
                        launchManager.ResetLaunchStatus();
                    }
                    else
                    {
                        if (launchManager.CheckResponsesUpdate() && updateLauncher)
                            launchManager.UpdateResponses();

                        await Task.Run(() =>
                        {
                            controller.StartProxy();
                        });

                        LaunchButton.Text = "RUNNING";
                        CosmosConsole.WriteLine("Proxy started!");

                        SettingsManager.ProxyStarted = true;
                        launchManager.OpenMinecraft();
                    }
                }
                else
                {
                    launchManager.ResetLaunchStatus();
                    StatusLabel.Text = "Error: Unable to connect to the Internet.\nBedrock Cosmos requires an active Internet connection to function.";
                }

                LaunchButton.Enabled = true;
            }
            else
            {
                CosmosConsole.WriteLine("Stopping proxy...");
                SettingsManager.ProxyStarted = false;

                launchManager.ResetLaunchStatus();
                controller.Stop();

                CosmosConsole.WriteLine("Proxy stopped!");
            }
        }

        private void AboutButton_Click(object sender, EventArgs e)
        {
            TabControl.SelectedTab = AboutPage;
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            TabControl.SelectedTab = SettingsPage;
        }

        private void AppIcon_Click(object sender, EventArgs e)
        {
            bool isDevMenuEnabled = SettingsManager.DevMenuCheck();
            if (isDevMenuEnabled)
            {
                AppIcon.Cursor = Cursors.Default;
                TabControl.SelectedTab = DevPage;
            }
        }

        private void ReturnToHomeScreen(object sender, EventArgs e)
        {
            if (sender == DevBackButton)
                AppIcon.Cursor = Cursors.Hand;

            TabControl.SelectedTab = HomePage;
        }

        private void OpenDiscordLink(object sender, EventArgs e)
        {
            Process.Start("https://www.discord.com/invite/hBujWqu");
        }

        private void OpenGitHubLink(object sender, EventArgs e)
        {
            Process.Start("https://github.com/Bedrock-Cosmos");
        }

        private void BackgroundModeToggle_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.BackgroundMode = BackgroundModeSwitch.Checked;

            if (SettingsManager.BackgroundMode)
            {
                LaunchButton.Enabled = false;
                LaunchButton.Text = "LISTENING";
                StatusLabel.Text = "Proxy is disabled.\nOpen Minecraft to start the service.";
                BackgroundModeTimer.Start();
                CosmosConsole.WriteLine("Background Mode enabled.");
            }
            else
            {
                LaunchButton.Enabled = true;

                if (SettingsManager.ProxyStarted)
                    LaunchButton.Text = "RUNNING";
                else
                    LaunchButton.Text = "LAUNCH";

                StatusLabel.Text = "";
                BackgroundModeTimer.Stop();
                CosmosConsole.WriteLine("Background Mode disabled.");
            }
        }

        private async void BackgroundModeTimer_Tick(object sender, EventArgs e)
        {
            Process[] pname = Process.GetProcessesByName("Minecraft.Windows");

            if (pname.Length != 0)
            {
                if (!SettingsManager.ProxyStarted)
                {
                    await launchManager.InternetCheck();

                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + @"Misc\CurrentVersion.json"))
                    {
                        launchManager.SetLatestVersions();
                        bool updateLauncher = launchManager.CheckLauncherUpdate();

                        if (updateLauncher && !SettingsManager.LauncherUpdatePrompted)
                        {
                            SettingsManager.LauncherUpdatePrompted = true;
                            TabControl.SelectedTab = UpdatePage;
                            Show();
                            WindowState = FormWindowState.Normal;
                            TrayIcon.Visible = false;
                        }
                        else
                        {
                            if (launchManager.CheckResponsesUpdate() && !updateLauncher)
                                launchManager.UpdateResponses();

                            await Task.Run(() =>
                            {
                                controller.StartProxy();
                            });

                            LaunchButton.Text = "LISTENING";
                            CosmosConsole.WriteLine("Proxy started!");

                            SettingsManager.ProxyStarted = true;
                            launchManager.UpdateLaunchButtonColor("purple");
                            StatusLabel.Text = "Proxy is enabled.\nClose Minecraft to stop the service.";
                        }
                    }
                    else
                    {
                        StatusLabel.Text = "Error: Unable to connect to the Internet.\nBedrock Cosmos requires an active Internet connection to function.";
                    }
                }
                //CosmosConsole.WriteLine("Minecraft is open.");
            }
            else
            {
                if (SettingsManager.ProxyStarted)
                {
                    SettingsManager.ProxyStarted = false;
                    launchManager.UpdateLaunchButtonColor("green");
                    StatusLabel.Text = "Proxy is disabled.\nOpen Minecraft to start the service.";
                    controller.Stop();
                }
                //CosmosConsole.WriteLine("Minecraft is closed.");
            }
        }

        private async void DownloadZipButton_Click(object sender, EventArgs e)
        {
            string fileUrl = "https://github.com/Bedrock-Cosmos/Responses/archive/refs/heads/main.zip";
            string downloadPath = AppDomain.CurrentDomain.BaseDirectory + @"main.zip";
            string extractPath = AppDomain.CurrentDomain.BaseDirectory;

            DownloadZipButton.Enabled = false;
            DownloadZipProgressLabel.Visible = true;

            try
            {
                DownloadZipProgressLabel.Text = "Downloading...";
                await asyncDownload.DownloadFileAsync(fileUrl, downloadPath);

                DownloadZipProgressLabel.Text = "Extracting...";
                await asyncDownload.ExtractFileAsync(downloadPath, extractPath, true);

                DownloadZipButton.Enabled = true;
                DownloadZipProgressLabel.Text = "Done!";
            }
            catch (Exception)
            {
                DownloadZipButton.Enabled = true;
                DownloadZipProgressLabel.Text = "Unable to download zip file.";
                CosmosConsole.WriteLine("Unable to download file. Download canceled.");
            }
        }

        private void EnableLoggingSwitch_CheckedChanged(object sender, EventArgs e)
        {
            if (EnableLoggingSwitch.Checked)
            {
                SettingsManager.EnableLogging = true;
                CosmosConsole.WriteLine("Logging enabled.");
            }
            else
            {
                CosmosConsole.WriteLine("Logging disabled.");
                SettingsManager.EnableLogging = false;
            }
        }

        private void ResetNewsButton_Click(object sender, EventArgs e)
        {
            //NewsManager.RetrieveNewsHistory();
            //NewsManager.RetrieveCurrentNews();
            //NewsManager.CheckForNews();
        }

        private void ClearLogsButton_Click(object sender, EventArgs e)
        {
            DevConsole.Text = "";
        }

        private void ExportLogsButton_Click(object sender, EventArgs e)
        {
            CosmosConsole.ExportLogs();
        }

        private void DisableDevMenuButton_Click(object sender, EventArgs e)
        {
            SettingsManager.DisableDevMenu();
            TabControl.SelectedTab = HomePage;
        }

        private void UpdateButton_Click(object sender, EventArgs e)
        {

        }

        private void CancelUpdateButton_Click(object sender, EventArgs e)
        {
            TabControl.SelectedTab = HomePage;
        }
    }
}
