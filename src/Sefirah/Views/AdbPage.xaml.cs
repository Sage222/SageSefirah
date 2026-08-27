using Sefirah.Data.Models;
using Sefirah.ViewModels;

namespace Sefirah.Views;

public sealed partial class AdbPage : Page
{
    public AdbViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<AdbViewModel>();

    public AdbPage()
    {
        InitializeComponent();
        Loaded += AdbPage_Loaded;
    }

    private async void AdbPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAppsAsync();
    }

    private async void RefreshApps_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAppsAsync();
    }

    private async void RunCommand_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RunCommandAsync();
    }

    private async void CommandInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ViewModel.RunCommandAsync();
        }
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: ComboBoxItem { Tag: string tag } } &&
            Enum.TryParse<AdbAppSortMode>(tag, out var mode))
        {
            ViewModel.SortMode = mode;
        }
    }

    /// <summary>
    /// Disabling a package can break device functionality, so we require
    /// explicit confirmation before disabling (matching the app's existing
    /// "ask for confirmation" pattern used for other destructive actions).
    /// Enabling a package does not require confirmation.
    /// </summary>
    private async void AppEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle || toggle.Tag is not AdbAppInfo app) return;

        var turningOn = toggle.IsOn;

        if (!turningOn)
        {
            var confirmDialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Disable app?",
                Content = $"Disabling \"{app.PackageName}\" may break functionality on this device. Continue?",
                PrimaryButtonText = "Disable",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                toggle.IsOn = true;
                return;
            }
        }

        await ViewModel.SetAppEnabledAsync(app, turningOn);
    }
}
