﻿using HidLibrary;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Semver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MagicStickUI
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const int VendorIdMagicStick = 0x2E8A; // Raspberry Pi VID
        private const int ProductIdMagicStick = 0xC010;
        private const ushort UsagePageVendorDefined = 0xFF00; // Usage Page (Vendor Defined 0xFF00)
        private const ushort UsageCharger = 0x14; // HID report USAGE (Charger)
        private const ushort UsagePageGenericDesktopControl = 0x01;
        private const ushort UsageVendorDefined = 0x00;
        private const double UpdatePollPeriodMsec = 5 * 1000; // 5 seconds

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<PresentationDevice> Devices { get; } = new();
        private readonly System.Timers.Timer _updateTimer = new();
        private PresentationDevice? _selectedDevice;
        private bool? _autoStart;
        private ProgressBarWindow? _pbw;
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly TrayIconManager _trayIcon;
        private Window _childWindow;

        //[DependsOn(nameof(SelectedDevice))] 
        public bool HasSelectedDevice => SelectedDevice != null;
        public bool HasNormalSelectedDevice => string.Equals(Constants.MagicStickFirmwareId, SelectedDevice?.FirmwareId, StringComparison.OrdinalIgnoreCase);


        public MainWindow(ILogger<MainWindow> logger, ILoggerFactory loggerFactory)
        {
            InitializeComponent();

            _logger = logger;
            _loggerFactory = loggerFactory;
            _trayIcon = new TrayIconManager(TaskbarIcon);

            _trayIcon.UpdateTaskbarIcon(null);

            DataContext = this;

            //Devices.CollectionChanged += (o, e) =>
            //{
            //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Devices)));
            //};

            PropertyChanged += OnPropertyChanged;

            ScanDevices();

            _updateTimer.Interval = UpdatePollPeriodMsec;
            _updateTimer.Elapsed += (s, e) => Application.Current.Dispatcher.Invoke(() =>
            {
                if (SelectedDevice != null)
                    UpdateDevice(SelectedDevice);
            });
            _updateTimer.Start();
        }

        
        public void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _logger.LogDebug($"OnPropertyChanged: {e.PropertyName}");

            if (e.PropertyName == nameof(SelectedDevice))
            {
                UpdateDevice(SelectedDevice);
            }
        }


        public bool AutoStart
        {
            get
            {
                if (_autoStart == null)
                {
                    var registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                    _autoStart = registryKey?.GetValue("MagicStickUI") != null;
                }

                return _autoStart ?? false;
            }
            set
            {
                var registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                if (registryKey == null)
                {
                    return;
                }

                if (value)
                {
                    var fileName = Process.GetCurrentProcess().MainModule?.FileName;
                    if (fileName != null) 
                        registryKey.SetValue("MagicStickUI", Path.Combine(AppContext.BaseDirectory, fileName));
                }
                else
                {
                    registryKey.DeleteValue("MagicStickUI", false);
                }

                _autoStart = value;
            }
        }

        
        public PresentationDevice? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (value != null)
                {
                    Properties.Settings.Default.LastSelectedDeviceId = value.DeviceId;
                    Properties.Settings.Default.Save();
                }
                else
                {
                    _trayIcon.UpdateTaskbarIcon(null);
                }

                _selectedDevice = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDevice)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSelectedDevice)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNormalSelectedDevice)));
            }
        }

        private void UpdateAllDevices()
        {
            foreach (var dev in Devices.ToList())
            {
                UpdateDevice(dev);
            }
        }

        private void UpdateDevice(PresentationDevice? dev)
        {
            if (dev == null)
                return;
            
            var hd = HidDevices.GetDevice(dev.ChargerDevicePathEndpoint);            
            if (hd is { IsConnected: true })
            {
                if ((dev.BatteryStatExpired || !dev.Connected))
                {
                    var ctrl = new DeviceCtrl(_loggerFactory.CreateLogger<DeviceCtrl>(),dev);
                    if (ctrl.TryHidReadBattery(out var value))
                    {
                        dev.BatteryPercentage = value;                        
                        dev.UpdateLastUpdateTimestamp();
                    }

                    // Also update the product name as versions change after firmware updates
                    if (hd.ReadProduct(out var product))
                        dev.DeviceName = GetHidString(product);
                }

                dev.Connected = true;
            }
            else
            {
                dev.Connected = false;
            }

            _trayIcon.UpdateTaskbarIcon(dev);
        }

        private void ScanDevices()
        {
            var hidDevices = HidDevices
                .Enumerate()
                .Where(t => t.Attributes.VendorId == VendorIdMagicStick && t.Attributes.ProductId == ProductIdMagicStick);

            // Group multiple HID endpoints by device serial (all belonging to the same device)
            var groupedHids = new Dictionary<string, List<HidDevice>>();
            foreach (var hd in hidDevices)
            {
                if (hd.ReadSerialNumber(out var value))
                {
                    var serial = GetHidString(value);

                    if (!groupedHids.ContainsKey(serial))
                        groupedHids[serial] = new List<HidDevice>();

                    groupedHids[serial].Add(hd);
                }
            }

            Devices.Clear();
            foreach (var group in groupedHids)
            {
                var dev = BuildPredentationDevice(group.Key, group.Value);
                Devices.Add(dev);
            }

            var found = Devices.FirstOrDefault(t => t.DeviceId == Properties.Settings.Default.LastSelectedDeviceId);
            SelectedDevice = found ?? Devices.FirstOrDefault();
        }

        private PresentationDevice BuildPredentationDevice(string serial, List<HidDevice> hids)
        {
            var chargerEp = hids.First(t =>
                (ushort)t.Capabilities.UsagePage == UsagePageVendorDefined && (ushort)t.Capabilities.Usage == UsageCharger);
            var controlEp = hids.First(t =>
                (ushort)t.Capabilities.UsagePage == UsagePageGenericDesktopControl && (ushort)t.Capabilities.Usage == UsageVendorDefined);

            var dev = new PresentationDevice();

            dev.ChargerDevicePathEndpoint = chargerEp.DevicePath;
            dev.ControlDevicePathEndpoint = controlEp.DevicePath;
            chargerEp.ReadProduct(out var product);
            dev.DeviceName = GetHidString(product);
            dev.DeviceVersion = (ushort)chargerEp.Attributes.Version;
            dev.ChargerDevicePathEndpoint = chargerEp.DevicePath;
            dev.ControlDevicePathEndpoint = controlEp.DevicePath;           
            dev.DeviceId = serial;
            dev.DeviceSerialNumber = serial;

            var semVerInfo = Util.GetSemVerFromDeviceName(dev.DeviceName);
            dev.FirmwareId = semVerInfo.Item1;
            dev.FirmwareSemVer = semVerInfo.Item2;

            var ctrl = new DeviceCtrl(_loggerFactory.CreateLogger<DeviceCtrl>(),dev);
            if (ctrl.TryHidReadBattery(out var battery))
                dev.BatteryPercentage = battery;

            return dev;
        }        
       
        private string GetHidString(byte[] bytes)
        {
            var str = string.Empty;
            foreach (var b in bytes)
                if (b > 0)
                    str += ((char)b).ToString();
            return str;
        }

        #region EventHandlers
        private void ExitButton_OnClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void DeviceSelect_OnClick(object sender, RoutedEventArgs e)
        {
            var mi = sender as MenuItem;
            if (mi == null)
                return;

            SelectedDevice = (PresentationDevice)mi.DataContext;
            e.Handled = true;
        }

        private void ScanDevices_OnClick(object sender, RoutedEventArgs e)
        {
            ScanDevices();
        }

        private void About_OnClick(object sender, RoutedEventArgs e)
        {
            var version = typeof(MainWindow).Assembly.GetName().Version;

            var sb = new StringBuilder();
            sb.AppendLine($"MagicStickUI v.{version?.Major}.{version?.Minor}.{version?.Build}.");

            MessageBox.Show(this, sb.ToString(), "MagicStickUI");
        }        

        private void GetInfo_OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectedDevice == null)
                return;

            if (_childWindow != null)
            {
                _childWindow.Close();
                _childWindow = null;
            }

            _childWindow = new DeviceInfoWindow(SelectedDevice);
            _childWindow.Owner = this;
            _childWindow.ShowDialog();
        }

        private void DeviceSettings_OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectedDevice == null)
                return;

            if (_childWindow != null)
            {
                _childWindow.Close();
                _childWindow = null;
            }

            _childWindow = new DeviceSettingsWindow(_loggerFactory.CreateLogger<DeviceSettingsWindow>(), _loggerFactory, SelectedDevice);
            _childWindow.Owner = this;
            _childWindow.ShowDialog();
        }

        private async void CheckUpdate_OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectedDevice == null)
                return;

            Release? msRelease;
            try
            {
                msRelease = await AzureUtil.GetLatestRelease(SelectedDevice.DeviceSerialNumber, Constants.MagicStickFirmwareId);
            }
            catch (Exception m)
            {
                MessageBox.Show($"Update cancelled. Failed to check for updates ({m.Message}).", 
                    Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            var latestVersion = SemVersion.Parse(msRelease?.SemVer, SemVersionStyles.Any);

            if (SelectedDevice.FirmwareSemVer.ComparePrecedenceTo(latestVersion) < 0 
                || SelectedDevice.FirmwareId == Constants.MagicStickInitFirmwareId
                || SelectedDevice.FirmwareSemVer.MetadataIdentifiers.Any(t => string.Equals(t, "debug", StringComparison.OrdinalIgnoreCase)))
            {

                var msg = $"Your current firmware version is {SelectedDevice.FirmwareSemVer}. Update to the latest firmware version {latestVersion}?";
                if (SelectedDevice.FirmwareId == Constants.MagicStickInitFirmwareId)
                    msg = $"Flash device to the latest firmware version {latestVersion}?";

                var res = MessageBox.Show(msg, Constants.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (res == MessageBoxResult.Yes)
                {
                    var updateCancellationTokenSource = new CancellationTokenSource();
                    _pbw = new ProgressBarWindow();
                    _pbw.Title = Constants.AppName;
                    _pbw.Owner = this;                    
                    _pbw.Closed += (o, a) => updateCancellationTokenSource.Cancel();

                    if (SelectedDevice.FirmwareId != Constants.MagicStickInitFirmwareId)
                        _pbw.SetUserText("Please press [Fn] + [Right Shift] + [Eject or Lock] to begin...");
                    else
                        _pbw.SetUserText("Please unplug the device and re-connect it in BOOTSEL mode to begin...");

                    _pbw.Show();

                    try
                    {
                        var piRoot = await Util.GetRpDriveRoot(60, updateCancellationTokenSource.Token);
                        if (piRoot == null)
                        {
                            _pbw.Close();
                            MessageBox.Show("Failed to detect device in update mode.", 
                                Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        _pbw.SetUserText($"Flashing: {msRelease.SemVer}...");

                        var deviceId = SelectedDevice.DeviceId;
                        var downloadUri = string.Format(Properties.Settings.Default.DownloadUriTemplate, deviceId, msRelease.Filename);
                        await Util.DownloadFileAsync(downloadUri,
                            Path.Combine(piRoot, msRelease.Filename), p =>
                            {
                                _pbw.SetProgress((int)p);
                            });

                        _pbw.Close();
                        MessageBox.Show("Update completed successfully.", 
                            Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (TaskCanceledException)
                    {
                        MessageBox.Show("Update cancelled.", 
                            Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception m)
                    {
                        _pbw.Close();
                        MessageBox.Show($"Update failed. {m.Message}.", 
                            Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Your device is running the latest available firmware.",
                    Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void InitializeDevice_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var updateCancellationTokenSource = new CancellationTokenSource();
                _pbw = new ProgressBarWindow();
                _pbw.Title = Constants.AppName;
                _pbw.Owner = this;
                _pbw.Closed += (o, a) => updateCancellationTokenSource.Cancel();
                _pbw.SetUserText("Please connect a device in BOOTSEL mode to continue...");
                _pbw.Show();

                var piRoot = await Util.GetRpDriveRoot(60, updateCancellationTokenSource.Token);
                if (piRoot == null)
                {
                    _pbw.Close();
                    MessageBox.Show("Failed to detect a device in BOOTSEL mode. Please consult the user manual.", 
                        Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _pbw.SetUserText("Flashing device initialization firmware...");

                const string fileName = $"{Constants.MagicStickInitFirmwareId}-latest.uf2";
                var downloadUri = string.Format(Properties.Settings.Default.DownloadUriTemplate, "0000000000000000", fileName);
                await Util.DownloadFileAsync(downloadUri,
                    Path.Combine(piRoot, fileName), p =>
                    {
                        _pbw.SetProgress((int)p);
                    });

                _pbw.Close();
                MessageBox.Show("Initialization completed successfully.", 
                    Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (TaskCanceledException)
            {
                MessageBox.Show("Initialization cancelled.", 
                    Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception m)
            {
                _pbw.Close();
                MessageBox.Show($"Initialization failed. {m.Message}.", 
                    Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        #endregion
    }

    public class PresentationDeviceIsSelectedConverter : IMultiValueConverter
    {
        // OneWay
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return (values[0] as PresentationDevice)?.DeviceId == (values[1] as PresentationDevice)?.DeviceId;
        }

        // TwoWay
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
