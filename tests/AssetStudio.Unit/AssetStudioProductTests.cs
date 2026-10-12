using AssetStudio;

namespace AssetStudio.Unit;

public sealed class AssetStudioProductTests
{
    [Test]
    public async Task Product_metadata_declares_graphical_profile_and_authoring_modes()
    {
        await Assert.That(AssetStudioProductInfo.ApplicationName)
            .IsEqualTo("Asset Studio");
        await Assert.That(AssetStudioProductInfo.GraphicalProfile)
            .IsEqualTo("Novolis.GraphicalProfile");
        await Assert.That(AssetStudioProductInfo.SupportedAuthoringModes)
            .Contains("Image/Sprite");
        await Assert.That(AssetStudioProductInfo.SupportedAuthoringModes)
            .Contains("Frame");
        await Assert.That(AssetStudioProductInfo.SupportedAuthoringModes)
            .Contains("Lightsaber Blade");
        await Assert.That(AssetStudioProductInfo.SupportedAuthoringModes)
            .Contains("Neon Sign");
        await Assert.That(AssetStudioProductInfo.SupportedAuthoringModes)
            .Contains("Glass");
        await Assert.That(AssetStudioProductInfo.SupportedAuthoringModes)
            .Contains("Orbital Reusable Rocket");
    }
}
