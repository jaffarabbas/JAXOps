using System.Windows;
using System.Windows.Media;

namespace DeploymentTool;

public partial class App : Application
{
	public void ApplyTheme(bool isDarkMode)
	{
		Resources["WindowBackgroundBrush"] = CreateBrush(isDarkMode ? "#111827" : "#F0F2F5");
		Resources["SurfaceBrush"] = CreateBrush(isDarkMode ? "#1F2937" : "#FFFFFF");
		Resources["SurfaceAltBrush"] = CreateBrush(isDarkMode ? "#0F172A" : "#111827");
		Resources["SurfaceMutedBrush"] = CreateBrush(isDarkMode ? "#0B1220" : "#F9FAFB");
		Resources["BorderBrushTheme"] = CreateBrush(isDarkMode ? "#334155" : "#DDE1E7");
		Resources["InputBorderBrush"] = CreateBrush(isDarkMode ? "#475569" : "#D1D5DB");
		Resources["TextPrimaryBrush"] = CreateBrush(isDarkMode ? "#E5E7EB" : "#1F2937");
		Resources["TextSecondaryBrush"] = CreateBrush(isDarkMode ? "#CBD5E1" : "#444444");
		Resources["TextMutedBrush"] = CreateBrush(isDarkMode ? "#94A3B8" : "#6B7280");
		Resources["TextFaintBrush"] = CreateBrush(isDarkMode ? "#64748B" : "#9CA3AF");
		Resources["InputBackgroundBrush"] = CreateBrush(isDarkMode ? "#0F172A" : "#FFFFFF");
		Resources["ButtonSecondaryBackgroundBrush"] = CreateBrush(isDarkMode ? "#1E293B" : "#F8F8F8");
		Resources["ButtonSecondaryForegroundBrush"] = CreateBrush(isDarkMode ? "#E5E7EB" : "#1F2937");
		Resources["DataGridRowHighlightBrush"] = CreateBrush(isDarkMode ? "#1E293B" : "#FFFBEB");
		Resources["TreeNewBrush"] = CreateBrush(isDarkMode ? "#052E16" : "#F0FDF4");
		Resources["TreeModifiedBrush"] = CreateBrush(isDarkMode ? "#3F2A06" : "#FFFBEB");
		Resources["LogPanelBrush"] = CreateBrush(isDarkMode ? "#020617" : "#111827");
		Resources["LogInputBrush"] = CreateBrush(isDarkMode ? "#111827" : "#1F2937");
		Resources["TabBackgroundBrush"] = CreateBrush(isDarkMode ? "#0F172A" : "#FFFFFF");
	}

	private static SolidColorBrush CreateBrush(string hex)
	{
		var converter = new BrushConverter();
		return (SolidColorBrush)converter.ConvertFromString(hex)!;
	}
}
