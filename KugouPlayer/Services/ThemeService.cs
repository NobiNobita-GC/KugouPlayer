using System.Windows;
using System.Windows.Media;

namespace KugouPlayer.Services;

public static class ThemeService
{
    private static readonly Dictionary<string, string> LightPalette = new()
    {
        ["WindowBackgroundBrush"] = "#F6F8FB",
        ["SurfaceBrush"] = "#FFFFFF",
        ["SurfaceMutedBrush"] = "#F1F4F8",
        ["PrimaryLightBrush"] = "#EAF5FF",
        ["ControlHoverBrush"] = "#EDF2F7",
        ["ControlPressedBrush"] = "#E2E9F0",
        ["TextPrimaryBrush"] = "#171A21",
        ["TextSecondaryBrush"] = "#656B76",
        ["TextTertiaryBrush"] = "#9AA0AA",
        ["DividerBrush"] = "#E9ECF1"
    };

    private static readonly Dictionary<string, string> DarkPalette = new()
    {
        ["WindowBackgroundBrush"] = "#17191E",
        ["SurfaceBrush"] = "#202329",
        ["SurfaceMutedBrush"] = "#2A2E35",
        ["PrimaryLightBrush"] = "#173A5B",
        ["ControlHoverBrush"] = "#30343C",
        ["ControlPressedBrush"] = "#383D47",
        ["TextPrimaryBrush"] = "#F2F4F7",
        ["TextSecondaryBrush"] = "#B6BBC4",
        ["TextTertiaryBrush"] = "#858C97",
        ["DividerBrush"] = "#343943"
    };

    public static void Apply(string themeMode)
    {
        var palette = themeMode == "深色" ? DarkPalette : LightPalette;
        foreach (var (key, color) in palette)
        {
            System.Windows.Application.Current.Resources[key] = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        }
    }
}
