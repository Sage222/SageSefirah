using System.Collections.ObjectModel;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;
using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;

namespace Sefirah.ViewModels;

public enum AdbAppSortMode
{
    NameAscending,
    NameDescending,
    EnabledFirst,
    DisabledFirst
}

/// <summary>
/// Backs the ADB tab: lists installed packages via `pm list packages`,
/// toggles enable/disable state, and runs arbitrary shell commands against
/// the currently active paired device's ADB session.
/// </summary>
public partial class AdbViewModel : ObservableObject
{
    private IAdbService AdbService { get; } = Ioc.Default.GetRequiredService<IAdbService>();
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();

    public ObservableCollection<AdbAppInfo> InstalledApps { get; } = [];

    private string commandInput = string.Empty;
    public string CommandInput
    {
        get => commandInput;
        set => SetProperty(ref commandInput, value);
    }

    private string commandOutput = string.Empty;
    public string CommandOutput
    {
        get => commandOutput;
        set => SetProperty(ref commandOutput, value);
    }

    private bool isBusy;
    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    private AdbAppSortMode sortMode = AdbAppSortMode.NameAscending;
    public AdbAppSortMode SortMode
    {
        get => sortMode;
        set
        {
            if (SetProperty(ref sortMode, value))
            {
                ApplySort();
            }
        }
    }

    /// <summary>
    /// Resolves the live ADB DeviceData for the currently active paired device,
    /// using the same matching logic AdbService uses internally (AndroidId first,
    /// falling back to a model-name match for devices not yet linked).
    /// </summary>
    private DeviceData? GetActiveDeviceData()
    {
        var activeDevice = DeviceManager.ActiveDevice;
        if (activeDevice is null) return null;

        var adbDevice = AdbService.AdbDevices.FirstOrDefault(d =>
            d.IsOnline &&
            (
                (!string.IsNullOrEmpty(d.AndroidId) && d.AndroidId == activeDevice.Id) ||
                (string.IsNullOrEmpty(d.AndroidId) &&
                 !string.IsNullOrEmpty(d.Model) &&
                 !string.IsNullOrEmpty(activeDevice.Model) &&
                 (activeDevice.Model.Equals(d.Model, StringComparison.OrdinalIgnoreCase) ||
                  activeDevice.Model.Contains(d.Model, StringComparison.OrdinalIgnoreCase) ||
                  d.Model.Contains(activeDevice.Model, StringComparison.OrdinalIgnoreCase)))
            ));

        return adbDevice?.DeviceData;
    }

    public async Task RefreshAppsAsync()
    {
        var deviceData = GetActiveDeviceData();
        if (deviceData is null) return;

        IsBusy = true;
        try
        {
            var enabledOutput = await RunRawCommandAsync(deviceData.Value, "pm list packages -e");
            var disabledOutput = await RunRawCommandAsync(deviceData.Value, "pm list packages -d");

            var apps = new List<AdbAppInfo>();
            apps.AddRange(ParsePackageList(enabledOutput, isEnabled: true));
            apps.AddRange(ParsePackageList(disabledOutput, isEnabled: false));

            await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                InstalledApps.Clear();
                foreach (var app in apps)
                {
                    InstalledApps.Add(app);
                }
            });

            ApplySort();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static IEnumerable<AdbAppInfo> ParsePackageList(string output, bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(output)) yield break;

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("package:", StringComparison.Ordinal))
            {
                yield return new AdbAppInfo(trimmed["package:".Length..], isEnabled);
            }
        }
    }

    /// <summary>
    /// Enables or disables a package. Callers are expected to have already
    /// confirmed disabling with the user (see AdbPage.xaml.cs) since disabling
    /// system packages can break the device.
    /// </summary>
    public async Task<bool> SetAppEnabledAsync(AdbAppInfo app, bool enabled)
    {
        var deviceData = GetActiveDeviceData();
        if (deviceData is null) return false;

        var command = enabled
            ? $"pm enable {app.PackageName}"
            : $"pm disable-user --user 0 {app.PackageName}";

        await RunRawCommandAsync(deviceData.Value, command);
        await RefreshAppsAsync();
        return true;
    }

    public async Task RunCommandAsync()
    {
        var deviceData = GetActiveDeviceData();
        if (deviceData is null || string.IsNullOrWhiteSpace(CommandInput)) return;

        IsBusy = true;
        try
        {
            var executed = CommandInput;
            var output = await RunRawCommandAsync(deviceData.Value, executed);
            CommandOutput = $"$ {executed}\n{output}\n\n{CommandOutput}";
            CommandInput = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string> RunRawCommandAsync(DeviceData deviceData, string command)
    {
        try
        {
            ConsoleOutputReceiver receiver = new();
            await AdbService.AdbClient.ExecuteShellCommandAsync(deviceData, command, receiver);
            return receiver.ToString().Trim();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private void ApplySort()
    {
        var sorted = SortMode switch
        {
            AdbAppSortMode.NameAscending => InstalledApps.OrderBy(a => a.PackageName, StringComparer.OrdinalIgnoreCase),
            AdbAppSortMode.NameDescending => InstalledApps.OrderByDescending(a => a.PackageName, StringComparer.OrdinalIgnoreCase),
            AdbAppSortMode.EnabledFirst => InstalledApps.OrderByDescending(a => a.IsEnabled).ThenBy(a => a.PackageName, StringComparer.OrdinalIgnoreCase),
            AdbAppSortMode.DisabledFirst => InstalledApps.OrderBy(a => a.IsEnabled).ThenBy(a => a.PackageName, StringComparer.OrdinalIgnoreCase),
            _ => InstalledApps.AsEnumerable()
        }.ToList();

        InstalledApps.Clear();
        foreach (var app in sorted)
        {
            InstalledApps.Add(app);
        }
    }
}
