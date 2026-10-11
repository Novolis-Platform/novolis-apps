using System.Diagnostics;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Reach.UiTests;

internal static class ReachAppiumAssertions
{
    internal static string ReadText(ISearchContext driver, string automationId) =>
        driver.FindElement(MobileBy.AccessibilityId(automationId)).Text;

    internal static async Task WaitForTextAsync(
        ISearchContext driver,
        string automationId,
        Func<string, bool> predicate,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        ArgumentNullException.ThrowIfNull(predicate);

        var deadline = Stopwatch.GetTimestamp()
            + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < deadline)
        {
            try
            {
                if (predicate(ReadText(driver, automationId)))
                {
                    return;
                }
            }
            catch (WebDriverException)
            {
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Timed out waiting for {automationId} to report the expected state.");
    }
}
