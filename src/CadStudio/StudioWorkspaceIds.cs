namespace CadStudio;

internal static class StudioWorkspaceIds
{
    public const string Draft2D = "draft2d";
    public const string Draft3D = "draft3d";
    public const string Model = "model";
    public const string Stage = "stage";
    public const string Ship = "ship";

    public static StudioWorkspace Parse(string? raw) =>
        (raw ?? "").Trim().ToLowerInvariant() switch
        {
            Draft3D or "3d" or "modeling" or "preview" => StudioWorkspace.Draft3D,
            Model or "mesh" => StudioWorkspace.Model,
            Stage or "render" or "stage/render" => StudioWorkspace.Stage,
            Ship or "shipdesign" or "ship-designer" => StudioWorkspace.Ship,
            _ => StudioWorkspace.Draft2D,
        };

    public static string ToId(StudioWorkspace w) => w switch
    {
        StudioWorkspace.Draft3D => Draft3D,
        StudioWorkspace.Model => Model,
        StudioWorkspace.Stage => Stage,
        StudioWorkspace.Ship => Ship,
        _ => Draft2D,
    };

    public static string ToDisplay(StudioWorkspace w) => w switch
    {
        StudioWorkspace.Draft3D => "Draft 3D",
        StudioWorkspace.Model => "Model",
        StudioWorkspace.Stage => "Stage / Render",
        StudioWorkspace.Ship => "Ship",
        _ => "Draft 2D",
    };
}
