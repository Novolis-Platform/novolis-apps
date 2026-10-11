using Avalonia.Automation;
using Avalonia.Controls;
using Novolis.Avalonia.Reach;

namespace Reach.Avalonia.Unit;

public sealed class ReachBrandingTests
{
    [Test]
    public async Task Brand_identity_is_shared_by_the_native_client_surface()
    {
        var header = ReachBranding.BuildHeader();

        await Assert.That(ReachBranding.ProductName).IsEqualTo("Novolis Reach");
        await Assert.That(ReachBranding.ShortName).IsEqualTo("Reach");
        await Assert.That(ReachBranding.Tagline).Contains("Private-network");
        await Assert.That(
                header.GetValue(AutomationProperties.AutomationIdProperty))
            .IsEqualTo("ReachBrandHeader");
        await Assert.That(header).IsTypeOf<StackPanel>();
        await Assert.That(
                ((StackPanel)header).Children
                    .OfType<Border>()
                    .Any(child =>
                        child.GetValue(
                            AutomationProperties.AutomationIdProperty)
                        == "ReachBrandMark"))
            .IsTrue();
    }
}
