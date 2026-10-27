using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FreeBudsController
{
    public class AppSettings
    {
        public string MacAddress { get; set; } = "0C:17:73:61:85:E6";
        public bool MinimizeToTray { get; set; } = true;
        public bool AutoRefreshOnOpen { get; set; } = true;
        public bool StartMinimized { get; set; } = false;

        public static void SetStartupWithWindows(bool enable)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    string appPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (enable && !string.IsNullOrEmpty(appPath))
                    {
                        key.SetValue("FreeBudsController", $"\"{appPath}\" --minimized");
                    }
                    else
                    {
                        key.DeleteValue("FreeBudsController", false);
                    }
                }
            }
            catch { }
        }

        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FreeBudsController");

        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null) return settings;
                }
            }
            catch { }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                {
                    Directory.CreateDirectory(SettingsDir);
                }
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }
    }

    public class FreeBudsService
    {
        private static readonly int[] PortCandidates = new[] { 1, 15, 16, 2, 3, 4, 5 };

        public async Task<Socket?> ConnectAsync(string macAddress, int timeoutMs = 2500)
        {
            ulong mac = BluetoothEndPoint.ParseMacAddress(macAddress);
            if (mac == 0) return null;

            foreach (int port in PortCandidates)
            {
                try
                {
                    var sock = new Socket((AddressFamily)32, SocketType.Stream, (ProtocolType)3)
                    {
                        SendTimeout = timeoutMs,
                        ReceiveTimeout = timeoutMs
                    };

                    var ep = new BluetoothEndPoint(mac, port);

                    // Use Task.Run to ensure non-blocking connect on background thread
                    await Task.Run(() => sock.Connect(ep)).WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));

                    if (sock.Connected)
                    {
                        return sock;
                    }
                    sock.Dispose();
                }
                catch
                {
                    // Try next port
                }
            }

            return null;
        }

        public async Task<bool> SetAncModeAsync(string macAddress, AncMode mode, AncLevel? level = null)
        {
            byte[] packet = FreeBudsProtocol.BuildSetAncPacket(mode, level);

            using var socket = await ConnectAsync(macAddress);
            if (socket == null) return false;

            try
            {
                await socket.SendAsync(packet, SocketFlags.None);
                await Task.Delay(250);

                // Drain optional response
                byte[] buffer = new byte[256];
                if (socket.Poll(500000, SelectMode.SelectRead))
                {
                    await socket.ReceiveAsync(buffer, SocketFlags.None);
                }

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { socket.Close(); } catch { }
            }
        }

        public async Task<(AncStatus? Anc, BatteryStatus? Battery)> QueryStatusAsync(string macAddress)
        {
            using var socket = await ConnectAsync(macAddress);
            if (socket == null) return (null, null);

            try
            {
                byte[] ancQuery = FreeBudsProtocol.BuildGetAncPacket();
                await socket.SendAsync(ancQuery, SocketFlags.None);
                await Task.Delay(350);

                byte[] buffer = new byte[1024];
                int received = await socket.ReceiveAsync(buffer, SocketFlags.None);

                byte[] data = new byte[received];
                Array.Copy(buffer, data, received);

                var anc = FreeBudsProtocol.ParseAncStatus(data);
                var battery = FreeBudsProtocol.ParseBatteryStatus(data);

                return (anc, battery);
            }
            catch
            {
                return (null, null);
            }
            finally
            {
                try { socket.Close(); } catch { }
            }
        }

        public async Task<string?> AutoDetectMacAddressAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = "-NoProfile -Command \"Get-PnpDevice -Class Bluetooth | Select-Object FriendlyName, DeviceID | ConvertTo-Json\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc == null) return null;

                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();

                    // Search for FreeBuds or HUAWEI DEV_XXXXXXXXXXXX
                    var match = Regex.Match(output, @"DEV_([0-9A-Fa-f]{12})", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        string hex = match.Groups[1].Value.ToUpper();
                        return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
                    }
                }
                catch { }

                return null;
            });
        }
    }
}
