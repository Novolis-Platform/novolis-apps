namespace AssetStudio;

/// <summary>
/// Release metadata exposed to smoke tests and host integration tooling.
/// </summary>
public static class AssetStudioProductInfo
{
    public const string ApplicationName = "Asset Studio";

    public const string GraphicalProfile = "Novolis.GraphicalProfile";

    public const string SupportedAuthoringModes =
        "Material,Geometry,Light,Effect,Image/Sprite,Scene/Assembly,Frame";
}
