using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LXLauncher
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "LocalizableElement")]
    public sealed partial class LxLauncher : Form
    {
        private const string ApiUrl = "https://lightning-x-api.vercel.app/api/counter";
        private const string ChangeLogUrl = "https://99anvar99.github.io/Changelog";
        private const string LxWebsiteUrl = "https://lightning-x.vercel.app/";
        private const string currentLxLauncherVersion = @"4.0.1";
        private const string LXLauncher = @"LXLauncher v" + currentLxLauncherVersion;

        // P/Invoke constants
        private const int WmNchittest = 0x84;
        private const int Htcaption = 0x2;

        private const string LatestVersionStartTag = "<p id=\"latest_version\">";
        private const string LatestVersionEndTag = "</p>";
        private const string MenuStatusStartTag = "<p id=\"menu_status\">";
        private const string MenuStatusEndTag = "</p>";
        private const string LauncherVersionStartTag = "<p id=\"latest_lx_launcher_version\">";
        private const string LauncherVersionEndTag = "</p>";

        private static readonly HttpClient Client = new HttpClient();

        private readonly string _lxDir;

        private readonly string[] _websites =
            { ApiUrl, LxWebsiteUrl, ChangeLogUrl };

        public LxLauncher()
        {
            InitializeComponent();

            _lxDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lightning X");
            var headerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Path.Combine(_lxDir, "Headers"));
            if (!Directory.Exists(_lxDir)) Directory.CreateDirectory(_lxDir);
            if (!Directory.Exists(headerPath)) Directory.CreateDirectory(headerPath);
            lx_launcher_version_label.Text = currentLxLauncherVersion;

            IncrementUserCount();

            SetPanelColorGradient(Menu_Panel, Color.FromArgb(77, 178, 255), Color.FromArgb(12, 55, 135));
            SetPanelColorGradient(Launcher_Panel, Color.FromArgb(255, 89, 89), Color.FromArgb(135, 0, 0));
            SetPanelColorGradient(Website_Panel, Color.FromArgb(219, 171, 88), Color.FromArgb(133, 84, 0));
            SetPanelColorGradient(Files_Panel, Color.FromArgb(147, 0, 222), Color.FromArgb(76, 41, 94));
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, int bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("dwmapi.dll", SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint dwAttribute, int[] pvAttribute,
            uint cbAttribute);

        // Helper function to set panel color gradient
        private static void SetPanelColorGradient(Panel panel, Color color1, Color color2)
        {
            panel.Paint += (sender, e) =>
            {
                var rect = new Rectangle(0, 0, panel.Width, panel.Height);
                var brush = new LinearGradientBrush(rect, color1, color2, LinearGradientMode.Horizontal);
                e.Graphics.FillRectangle(brush, rect);
            };
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WmNchittest)
                m.Result = (IntPtr)Htcaption; // Allow moving the window by clicking and dragging the client area
        }

        private async void IncrementUserCount()
        {
            try
            {
                var content = new StringContent("{\"action\":\"increment\"}", Encoding.UTF8, "application/json");
                var response = await Client.PostAsync(ApiUrl, content);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();

                var userCount = ExtractUserCount(result);
                user_count_int.Text = userCount;
            }
            catch (HttpRequestException httpEx)
            {
                MessageBox.Show($@"HTTP Error: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"Error: {ex.Message}");
            }
        }

        private static string ExtractUserCount(string jsonResponse)
        {
            var startIndex = jsonResponse.IndexOf(':') + 1;
            var endIndex = jsonResponse.IndexOf('}', startIndex);
            return jsonResponse.Substring(startIndex, endIndex - startIndex).Trim();
        }

        private static async Task DecrementUserCount()
        {
            try
            {
                var content = new StringContent("{\"action\":\"decrement\"}", Encoding.UTF8, "application/json");
                var response = await Client.PostAsync(ApiUrl, content);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                if (int.Parse(ExtractUserCount(result)) < 0)
                {
                    content = new StringContent("{\"action\":\"reset\"}", Encoding.UTF8, "application/json");
                    response = await Client.PostAsync(ApiUrl, content);
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (HttpRequestException httpEx)
            {
                MessageBox.Show($@"HTTP Error: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"Error: {ex.Message}");
            }
        }

        private async void user_count_timer_Tick(object sender, EventArgs e)
        {
            try
            {
                var response = await Client.GetAsync(ApiUrl);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();
                var userCount = ExtractUserCount(result);
                user_count_int.Text = userCount;
            }
            catch (HttpRequestException httpEx)
            {
                MessageBox.Show($@"HTTP Error: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"Error: {ex.Message}");
            }
        }

        private static void DeleteDirectory(string path, bool recursive)
        {
            if (recursive)
                foreach (var subdirectory in Directory.GetDirectories(path))
                    DeleteDirectory(subdirectory, true);

            foreach (var file in Directory.GetFiles(path))
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch (Exception)
                {
                    // ignored
                }

            Directory.Delete(path);
        }

        private static void DeleteFileIfExists(string filePath)
        {
            if (!File.Exists(filePath)) return;
            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
        }

        private async Task DownloadDllAsync()
        {
            try
            {
                download_progressbar.Visible = true;

                var lxDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Lightning X");
                var fontsPath = Path.Combine(lxDir, "Fonts");

                var tempPath = Path.Combine(lxDir, "temp");
                if (File.Exists(tempPath)) File.Delete(tempPath);

                download_progressbar.Value = 10;

                using (var wc = new WebClient())
                {
                    await wc.DownloadFileTaskAsync("https://codeload.github.com/Lightning-X/Files/zip/refs/heads/main",
                        tempPath);
                }

                download_progressbar.Value = 50;

                await Task.Delay(1);

                if (Directory.Exists(fontsPath)) DeleteDirectory(fontsPath, true);

                download_progressbar.Value = 60;

                await Task.Delay(1);

                DeleteFileIfExists(Path.Combine(lxDir, "version.txt"));
                DeleteFileIfExists(Path.Combine(lxDir, "LX.dll"));

                download_progressbar.Value = 70;

                await Task.Delay(1);

                var extractedPath = Path.Combine(lxDir, "Files-main");
                if (Directory.Exists(extractedPath)) DeleteDirectory(extractedPath, true);

                download_progressbar.Value = 80;

                ZipFile.ExtractToDirectory(tempPath, lxDir);
                File.Delete(tempPath);

                download_progressbar.Value = 90;

                await Task.Delay(1);

                ZipFile.ExtractToDirectory(Path.Combine(extractedPath, "Fonts.zip"), fontsPath);
                File.Move(Path.Combine(extractedPath, "LX.dll"), Path.Combine(lxDir, "LX.dll"));

                download_progressbar.Value = 95;

                await Task.Delay(1);

                DeleteDirectory(extractedPath, true);

                download_progressbar.Value = 100;

                download_progressbar.Visible = false;
                download_progressbar.Value = 0;

                await Task.Delay(300);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, LXLauncher, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task GetAllDataFromWebsiteTask()
        {
            try
            {
                var hasAcknowledgedWebsite = Read_Registry("has_acknowledged_website", "0");
                if (hasAcknowledgedWebsite == "0")
                {
                    Process.Start(new ProcessStartInfo("https://lightning-x.vercel.app/#/acknowledgement")
                        { UseShellExecute = true });
                    Write_Registry("has_acknowledged_website", "1");
                }

                // Validate websites concurrently
                var validationTasks = _websites.Select(async website =>
                {
                    var websitesResponse = await Client.GetAsync(website);
                    if (websitesResponse.StatusCode !=
                        HttpStatusCode.OK) // Use Invoke to show MessageBox safely in UI thread
                        Invoke((Action)(() => lx_server_label.Text = @"OFFLINE"));
                });

                await Task.WhenAll(validationTasks);

                // Fetch changelog and parse content
                var response = await Client.GetStringAsync(ChangeLogUrl);
                var latestMenuVersion = ExtractContent(response, LatestVersionStartTag, LatestVersionEndTag);
                var status = ExtractContent(response, MenuStatusStartTag, MenuStatusEndTag);
                var latestLXLauncherVersion = ExtractContent(response, LauncherVersionStartTag, LauncherVersionEndTag);

                if (string.IsNullOrEmpty(latestMenuVersion) || string.IsNullOrEmpty(status))
                {
                    Invoke((Action)(() =>
                    {
                        latest_version_int.Text = @"ERROR";
                        menu_status_label.Text = @"ERROR";
                        lx_server_label.Text = @"ERROR";
                    }));
                    return;
                }

                // Update UI labels on the UI thread
                Invoke((Action)(() =>
                {
                    latest_version_int.Text = latestMenuVersion;
                    menu_status_label.Text = status;
                    lx_server_label.Text = @"ONLINE";
                }));

                // Check if the launcher is up to date
                if (currentLxLauncherVersion != latestLXLauncherVersion)
                {
                    if (MessageBox.Show($"LXLauncher {latestLXLauncherVersion} is available. Would you like to download it?", LXLauncher, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        // Download the latest version of the launcher
                        Process.Start("https://github.com/Lightning-X/Files/releases/download/Launcher/LXLauncher.exe");
                        // Close the current launcher
                        Exit_Label_Click(null, null);
                    }
                }

                // Check if Menu is up to date (Read version from registry and compare it with latestVersion)
                var currentMenuVersion = Read_Registry("version", "0");
                if (currentMenuVersion != latestMenuVersion || !File.Exists(Path.Combine(_lxDir, "LX.dll")) ||
                    !Directory.Exists(Path.Combine(_lxDir, "Fonts")))
                {
                    _ = DownloadDllAsync();
                    
                    if (currentMenuVersion != latestMenuVersion)
                        Write_Registry("version", latestMenuVersion);
                }

                changelog_web_view.Refresh();
            }
            catch (HttpRequestException httpEx)
            {
                MessageBox.Show($@"HTTP Error: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"Error: {ex.Message}");
            }
        }

        private static string ExtractContent(string input, string startTag, string endTag)
        {
            try
            {
                var startIndex = input.IndexOf(startTag, StringComparison.Ordinal);
                if (startIndex == -1) return null;
                startIndex += startTag.Length;

                var endIndex = input.IndexOf(endTag, startIndex, StringComparison.Ordinal);
                return endIndex == -1 ? null : input.Substring(startIndex, endIndex - startIndex).Trim();
            }
            catch
            {
                return null;
            }
        }

        private void LxLauncher_Load(object sender, EventArgs e)
        {
            _ = GetAllDataFromWebsiteTask();
        }

        private void Exit_Label_Click(object sender, EventArgs e)
        {
            _ = DecrementUserCount();
            Thread.Sleep(300);
            Application.Exit();
        }

        private void launch_button_Click(object sender, EventArgs e)
        {
            // Check if GTA5.exe is running
            if (Process.GetProcessesByName("GTA5").Any())
            {
                MessageBox.Show("GTA 5 is already running!", LXLauncher, MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }

            var selectedLauncher = game_launch_combo_box.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedLauncher))
            {
                MessageBox.Show("Please select a launcher from the dropdown.", LXLauncher, MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }

            try
            {
                switch (selectedLauncher)
                {
                    case "Epic Games":
                        Process.Start(
                            "com.epicgames.launcher://apps/9d2d0eb64d5c44529cece33fe2a46482?action=launch&silent=true");
                        break;
                    case "Steam":
                        var steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null)
                            ?.ToString();
                        if (!string.IsNullOrWhiteSpace(steamPath))
                            Process.Start("steam://run/271590");
                        else
                            MessageBox.Show(
                                "Whoops, looks like Steam isn't installed. Try selecting a different launcher in the dropdown.",
                                LXLauncher, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        break;
                    case "Rockstar Games Launcher":
                        var installFolder = Registry.LocalMachine
                            .OpenSubKey(@"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V")
                            ?.GetValue("InstallFolder")?.ToString();
                        if (!string.IsNullOrEmpty(installFolder))
                            Process.Start(Path.Combine(installFolder, "PlayGTAV.exe"));
                        else
                            MessageBox.Show("Could not find the installation folder for GTA V.", LXLauncher,
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        break;
                    default:
                        MessageBox.Show("Unknown launcher selected.", LXLauncher, MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", LXLauncher, MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        private delegate IntPtr VirtualAllocExDelegate(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize,
            uint flAllocationType, uint flProtect);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        private delegate int WriteProcessMemoryDelegate(IntPtr hProcess, IntPtr lpBaseAddress, byte[] buffer, uint size,
            int lpNumberOfBytesWritten);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        private delegate IntPtr CreateRemoteThreadDelegate(IntPtr hProcess, IntPtr lpThreadAttribute,
            IntPtr dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        private static int GetPid()
        {
            return Process.GetProcessesByName("GTA5").FirstOrDefault()?.Id ?? -1;
        }

        private static void InjectDll(IntPtr pHandle, IntPtr hKernel32, IntPtr procAddress, string dllPath)
        {
            var virtualAllocEx = (VirtualAllocExDelegate)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hKernel32, "VirtualAllocEx"), typeof(VirtualAllocExDelegate));
            var writeProcessMemory = (WriteProcessMemoryDelegate)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hKernel32, "WriteProcessMemory"), typeof(WriteProcessMemoryDelegate));
            var createRemoteThread = (CreateRemoteThreadDelegate)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hKernel32, "CreateRemoteThread"), typeof(CreateRemoteThreadDelegate));

            var bytes = Encoding.Unicode.GetBytes(dllPath);
            var baseAddress = virtualAllocEx(pHandle, IntPtr.Zero, (IntPtr)bytes.Length, 12288u, 64u);

            if (baseAddress == IntPtr.Zero)
            {
                ShowError("Couldn't allocate memory in remote process (VirtualAllocEx)");
                return;
            }

            if (writeProcessMemory(pHandle, baseAddress, bytes, (uint)bytes.Length, 0) == 0)
            {
                ShowError("Couldn't write to remote process memory (WriteProcessMemory)");
                return;
            }

            if (createRemoteThread(pHandle, IntPtr.Zero, IntPtr.Zero, procAddress, baseAddress, 0u, IntPtr.Zero) == IntPtr.Zero)
            {
                ShowError("Couldn't create remote thread (CreateRemoteThread)");
            }
        }

        private void Inject_Button_Click(object sender, EventArgs e)
        {
            if (GetPid() == -1)
            {
                ShowError(@"Failed to find GTA process. Make sure GTA is running.");
                return;
            }

            var pHandle = OpenProcess(1082u, 1, (uint)GetPid());
            if (pHandle == IntPtr.Zero)
            {
                ShowError("Failed to open process. Make sure you are running LXLauncher with administrator");
                return;
            }

            var hKernel32 = GetModuleHandle("kernel32.dll");
            var procAddress = GetProcAddress(hKernel32, "LoadLibraryW");
            if (procAddress == IntPtr.Zero)
            {
                ShowError("Couldn't find LoadLibraryW.");
                return;
            }

            var dllPath = Path.Combine(_lxDir, "LX.dll");
            if (!File.Exists(dllPath))
            {
                MessageBox.Show(@"Required files not found. Re-opening LXLauncher to download required files!", LXLauncher, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Restart();
                return;
            }

            try
            {
                InjectDll(pHandle, hKernel32, procAddress, dllPath);
            }
            catch (IOException ex)
            {
                MessageBox.Show(@"The injection was most likely blocked by your antivirus! Error: " + ex.Message, LXLauncher, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(@"Failed to inject! Error: " + ex.Message, LXLauncher, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                CloseHandle(pHandle);
            }
        }

        #region Registry Methods

        private string Read_Registry(string name, string value)
        {
            const string subKeyPath = @"Software\LX";
            try
            {
                using (var lx = Registry.LocalMachine.OpenSubKey(subKeyPath, false))
                {
                    var val = lx?.GetValue(name)?.ToString();
                    if (val != null) return val;
                }

                using (var lx = Registry.LocalMachine.CreateSubKey(subKeyPath))
                {
                    lx?.SetValue(name, value);
                }

                return value;
            }
            catch (Exception)
            {
                return value;
            }
        }

        private static void Write_Registry(string name, string value)
        {
            const string subKeyPath = @"Software\LX";
            try
            {
                using (var lx = Registry.LocalMachine.CreateSubKey(subKeyPath))
                {
                    lx?.SetValue(name, value);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        #endregion

        private static void ShowInfo(string message)
        {
            MessageBox.Show(message, LXLauncher, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static bool ShowInfo (string message, MessageBoxButtons buttons)
        {
            return MessageBox.Show(message, LXLauncher, buttons, MessageBoxIcon.Information) == DialogResult.Yes;
        }

        private static void ShowWarning(string message)
        {
            MessageBox.Show(message, LXLauncher, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(message, LXLauncher, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}