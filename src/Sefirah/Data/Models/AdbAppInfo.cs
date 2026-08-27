namespace Sefirah.Data.Models;

/// <summary>
/// Lightweight model representing an installed package as reported by
/// `pm list packages` via ADB, independent of the phone-side companion app's
/// socket-based application list.
/// </summary>
public class AdbAppInfo(string packageName, bool isEnabled)
{
    public string PackageName { get; set; } = packageName;
    public bool IsEnabled { get; set; } = isEnabled;
}
