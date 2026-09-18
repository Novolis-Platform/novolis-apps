using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Windows;

namespace Merglyph.UiTests;

internal enum UiTestPlatform
{
    Android,
    Windows,
}

internal sealed class AppiumSession : IDisposable
{
    private readonly AppiumDriver _driver;

    private AppiumSession(AppiumDriver driver) => _driver = driver;

    public static AppiumSession StartFromEnvironment()
    {
        var platform = ParsePlatform(Environment.GetEnvironmentVariable("MERGLYPH_UI_PLATFORM"));
        var appPath = RequireExistingFile("MERGLYPH_UI_APP");

        return platform switch
        {
            UiTestPlatform.Android => new AppiumSession(StartAndroid(appPath)),
            UiTestPlatform.Windows => new AppiumSession(StartWindows(appPath)),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null),
        };
    }

    public AppiumElement FindByAutomationId(string automationId) =>
        _driver is WindowsDriver
            ? _driver.FindElement(MobileBy.AccessibilityId(automationId))
            : _driver.FindElement(MobileBy.Id(automationId));

    public void Dispose()
    {
        try
        {
            _driver.Quit();
        }
        finally
        {
            _driver.Dispose();
        }
    }

    private static AndroidDriver StartAndroid(string appPath)
    {
        var options = new AppiumOptions
        {
            PlatformName = "Android",
            AutomationName = "UIAutomator2",
            App = appPath,
        };
        options.AddAdditionalAppiumOption("appPackage", "dev.novolis.merglyph");
        options.AddAdditionalAppiumOption("appActivity", "dev.novolis.merglyph.MainActivity");

        return new AndroidDriver(options);
    }

    private static WindowsDriver StartWindows(string appPath)
    {
        var options = new AppiumOptions
        {
            PlatformName = "Windows",
            AutomationName = "windows",
            App = appPath,
        };

        return new WindowsDriver(options);
    }

    private static UiTestPlatform ParsePlatform(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "android" => UiTestPlatform.Android,
            "windows" => UiTestPlatform.Windows,
            _ => throw new InvalidOperationException(
                "Set MERGLYPH_UI_PLATFORM to 'android' or 'windows' before running UI tests."),
        };

    private static string RequireExistingFile(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Set {variable} to the built application path before running UI tests.");

        var fullPath = Path.GetFullPath(value);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException($"UI test application does not exist: {fullPath}", fullPath);
    }
}
