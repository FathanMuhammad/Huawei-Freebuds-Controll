using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace FreeBudsController
{
    public partial class MainWindow : Window
    {
        private readonly FreeBudsService _service = new();
        private AppSettings _settings;
        private NotifyIcon? _notifyIcon;
        private AncLevel _currentSelectedLevel = AncLevel.Dynamic;
        private bool _isUpdating = false;

        public MainWindow()
        {
            InitializeComponent();

            _settings = AppSettings.Load();
            TxtMacAddress.Text = _settings.MacAddress;

            InitializeTrayIcon();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            StateChanged += MainWindow_StateChanged;
            KeyDown += MainWindow_KeyDown;
        }

        private async void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F1 || e.Key == System.Windows.Input.Key.D1)
            {
                await SetModeAsync(AncMode.Off);
            }
            else if (e.Key == System.Windows.Input.Key.F2 || e.Key == System.Windows.Input.Key.D2)
            {
                await SetModeAsync(AncMode.Awareness);
            }
            else if (e.Key == System.Windows.Input.Key.F3 || e.Key == System.Windows.Input.Key.D3)
            {
                await SetModeAsync(AncMode.NoiseCancelling, _currentSelectedLevel);
            }
            else if (e.Key == System.Windows.Input.Key.F5)
            {
                await RefreshStatusAsync();
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (_settings.MinimizeToTray) Hide();
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            HighlightSelectedLevel(_currentSelectedLevel);
            if (_settings.AutoRefreshOnOpen && !string.IsNullOrWhiteSpace(_settings.MacAddress))
            {
                await RefreshStatusAsync();
            }
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _notifyIcon = new NotifyIcon
                {
                    Text = "FreeBuds SE4 ANC Controller",
                    Icon = SystemIcons.Application,
                    Visible = true
                };

                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("🔇 Off", null, async (_, _) => await SetModeAsync(AncMode.Off));
                contextMenu.Items.Add("👂 Awareness", null, async (_, _) => await SetModeAsync(AncMode.Awareness));
                contextMenu.Items.Add("🔕 Noise Cancelling", null, async (_, _) => await SetModeAsync(AncMode.NoiseCancelling, _currentSelectedLevel));
                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add("🪟 Buka Aplikasi", null, (_, _) => RestoreWindow());
                contextMenu.Items.Add("❌ Keluar", null, (_, _) => ExitApplication());

                _notifyIcon.ContextMenuStrip = contextMenu;
                _notifyIcon.DoubleClick += (_, _) => RestoreWindow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to init tray icon: {ex.Message}");
            }
        }

        private void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            Application.Current.Shutdown();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
            {
                Hide();
                _notifyIcon?.ShowBalloonTip(1500, "FreeBuds SE4 Controller", "Aplikasi diminimalkan ke System Tray.", ToolTipIcon.Info);
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_settings.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                ExitApplication();
            }
        }

        private async Task RefreshStatusAsync()
        {
            string mac = TxtMacAddress.Text.Trim();
            if (string.IsNullOrWhiteSpace(mac)) return;

            SetBusyState(true, "Memeriksa status...");

            var (anc, battery) = await _service.QueryStatusAsync(mac);

            if (anc != null)
            {
                TxtCurrentStatus.Text = anc.DisplayText;
                TxtStatusDetail.Text = $"Mode: {anc.ModeName} | Level: {anc.LevelName}";
                UpdateIndicators(anc.Mode);

                if (anc.Mode == AncMode.NoiseCancelling)
                {
                    _currentSelectedLevel = anc.Level;
                    HighlightSelectedLevel(anc.Level);
                }
            }
            else
            {
                TxtCurrentStatus.Text = "Tidak Terhubung";
                TxtStatusDetail.Text = "Pastikan Bluetooth aktif dan FreeBuds sedang dipakai.";
                UpdateIndicators(null);
            }

            if (battery != null && (battery.Left > 0 || battery.Right > 0))
            {
                PanelBattery.Visibility = Visibility.Visible;
                TxtBatteryInfo.Text = $"L: {battery.Left}% | R: {battery.Right}%" + (battery.Case > 0 ? $" | Case: {battery.Case}%" : "");
            }
            else
            {
                PanelBattery.Visibility = Visibility.Collapsed;
            }

            SetBusyState(false);
        }

        private async Task SetModeAsync(AncMode mode, AncLevel? level = null)
        {
            if (_isUpdating) return;
            string mac = TxtMacAddress.Text.Trim();
            if (string.IsNullOrWhiteSpace(mac))
            {
                MessageBox.Show("Silakan isi atau auto-detect MAC address FreeBuds terlebih dahulu.", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string modeName = mode switch
            {
                AncMode.Off => "Off",
                AncMode.Awareness => "Awareness",
                AncMode.NoiseCancelling => level.HasValue ? $"Noise Cancelling ({level.Value})" : "Noise Cancelling",
                _ => mode.ToString()
            };

            SetBusyState(true, $"Mengaktifkan {modeName}...");

            bool success = await _service.SetAncModeAsync(mac, mode, level);
            if (success)
            {
                TxtCurrentStatus.Text = modeName;
                TxtStatusDetail.Text = $"Berhasil diubah ke {modeName}";
                UpdateIndicators(mode);
            }
            else
            {
                TxtStatusDetail.Text = $"Gagal mengubah ke {modeName}. Coba cek koneksi Bluetooth.";
                MessageBox.Show("Gagal mengirim perintah ke FreeBuds. Pastikan TWS sedang aktif/terkoneksi.", "Error Koneksi", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            SetBusyState(false);
        }

        private void SetBusyState(bool isBusy, string? message = null)
        {
            _isUpdating = isBusy;
            BtnModeOff.IsEnabled = !isBusy;
            BtnModeAwareness.IsEnabled = !isBusy;
            BtnModeAnc.IsEnabled = !isBusy;
            BtnRefreshStatus.IsEnabled = !isBusy;

            if (message != null)
            {
                TxtStatusDetail.Text = message;
            }
        }

        private void UpdateIndicators(AncMode? activeMode)
        {
            IndicatorOff.Background = activeMode == AncMode.Off ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)) : System.Windows.Media.Brushes.Transparent;
            IndicatorAwareness.Background = activeMode == AncMode.Awareness ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)) : System.Windows.Media.Brushes.Transparent;
            IndicatorAnc.Background = activeMode == AncMode.NoiseCancelling ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)) : System.Windows.Media.Brushes.Transparent;

            if (activeMode == AncMode.Off)
            {
                TxtCurrentStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184));
            }
            else if (activeMode == AncMode.Awareness)
            {
                TxtCurrentStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
            }
            else if (activeMode == AncMode.NoiseCancelling)
            {
                TxtCurrentStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
            }
        }

        private void HighlightSelectedLevel(AncLevel level)
        {
            var inactiveBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59));
            var activeBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
            var inactiveFg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184));
            var activeFg = System.Windows.Media.Brushes.White;

            BtnLvlGeneral.Background = level == AncLevel.General ? activeBg : inactiveBg;
            BtnLvlGeneral.Foreground = level == AncLevel.General ? activeFg : inactiveFg;

            BtnLvlCozy.Background = level == AncLevel.Cozy ? activeBg : inactiveBg;
            BtnLvlCozy.Foreground = level == AncLevel.Cozy ? activeFg : inactiveFg;

            BtnLvlUltra.Background = level == AncLevel.Ultra ? activeBg : inactiveBg;
            BtnLvlUltra.Foreground = level == AncLevel.Ultra ? activeFg : inactiveFg;

            BtnLvlDynamic.Background = level == AncLevel.Dynamic ? activeBg : inactiveBg;
            BtnLvlDynamic.Foreground = level == AncLevel.Dynamic ? activeFg : inactiveFg;
        }

        private async void BtnModeOff_Click(object sender, RoutedEventArgs e) => await SetModeAsync(AncMode.Off);

        private async void BtnModeAwareness_Click(object sender, RoutedEventArgs e) => await SetModeAsync(AncMode.Awareness);

        private async void BtnModeAnc_Click(object sender, RoutedEventArgs e) => await SetModeAsync(AncMode.NoiseCancelling, _currentSelectedLevel);

        private async void BtnLvlGeneral_Click(object sender, RoutedEventArgs e)
        {
            _currentSelectedLevel = AncLevel.General;
            HighlightSelectedLevel(_currentSelectedLevel);
            await SetModeAsync(AncMode.NoiseCancelling, _currentSelectedLevel);
        }

        private async void BtnLvlCozy_Click(object sender, RoutedEventArgs e)
        {
            _currentSelectedLevel = AncLevel.Cozy;
            HighlightSelectedLevel(_currentSelectedLevel);
            await SetModeAsync(AncMode.NoiseCancelling, _currentSelectedLevel);
        }

        private async void BtnLvlUltra_Click(object sender, RoutedEventArgs e)
        {
            _currentSelectedLevel = AncLevel.Ultra;
            HighlightSelectedLevel(_currentSelectedLevel);
            await SetModeAsync(AncMode.NoiseCancelling, _currentSelectedLevel);
        }

        private async void BtnLvlDynamic_Click(object sender, RoutedEventArgs e)
        {
            _currentSelectedLevel = AncLevel.Dynamic;
            HighlightSelectedLevel(_currentSelectedLevel);
            await SetModeAsync(AncMode.NoiseCancelling, _currentSelectedLevel);
        }

        private async void BtnRefreshStatus_Click(object sender, RoutedEventArgs e) => await RefreshStatusAsync();

        private void BtnSaveMac_Click(object sender, RoutedEventArgs e)
        {
            string mac = TxtMacAddress.Text.Trim();
            if (string.IsNullOrWhiteSpace(mac)) return;

            _settings.MacAddress = mac;
            _settings.Save();
            MessageBox.Show("MAC address berhasil disimpan!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnAutoDetect_Click(object sender, RoutedEventArgs e)
        {
            TxtStatusDetail.Text = "Mencari perangkat Bluetooth...";
            string? detectedMac = await _service.AutoDetectMacAddressAsync();
            if (!string.IsNullOrWhiteSpace(detectedMac))
            {
                TxtMacAddress.Text = detectedMac;
                _settings.MacAddress = detectedMac;
                _settings.Save();
                TxtStatusDetail.Text = $"Ditemukan: {detectedMac}";
                await RefreshStatusAsync();
            }
            else
            {
                TxtStatusDetail.Text = "Tidak dapat menemukan MAC address otomatis.";
                MessageBox.Show("Perangkat FreeBuds tidak ditemukan via scanning otomatis. Silakan masukkan MAC address secara manual.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
