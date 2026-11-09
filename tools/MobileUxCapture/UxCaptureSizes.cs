namespace MobileUxCapture;

internal readonly record struct UxFormFactor(string Id, int Width, int Height)
{
    public static readonly UxFormFactor Phone = new("phone", 540, 1260);
    public static readonly UxFormFactor Desktop = new("desktop", 1280, 800);

    public static IReadOnlyList<UxFormFactor> All { get; } = [Phone, Desktop];
}
