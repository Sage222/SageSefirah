namespace Sefirah.Views;

// Additive partial-class file: adds ADB tab navigation without touching
// MainPage.xaml.cs's existing Pages dictionary / NavigationView_SelectionChanged
// switch. Wired via Tapped on the AdbNavigationItem in MainPage.xaml
// (kept independent of the shared SelectionChanged handler intentionally,
// since "Adb" is not registered in the existing Pages lookup).
public sealed partial class MainPage
{
    private void AdbNavigationItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(AdbPage));
    }
}
