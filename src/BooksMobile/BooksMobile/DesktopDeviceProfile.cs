namespace BooksMobile;

/// <summary>Desktop window geometry for Field UX validation.</summary>
public readonly record struct DesktopDeviceProfile(double Width, double Height, bool CanResize, string Name)
{
    public static DesktopDeviceProfile Default { get; } = new(1100, 720, true, "desktop");

    /// <summary>Flip8 main aspect at 50% DIPs — fits most monitors while preserving ~21:9.</summary>
    public static DesktopDeviceProfile Flip8 { get; } = new(
        MainWindow.Flip8Width,
        MainWindow.Flip8Height,
        CanResize: false,
        Name: "flip8");

    /// <summary>Native Flip8 main panel pixels as DIPs (may exceed small displays).</summary>
    public static DesktopDeviceProfile Flip8Native { get; } = new(
        MainWindow.Flip8NativeWidth,
        MainWindow.Flip8NativeHeight,
        CanResize: false,
        Name: "flip8-native");

    public static DesktopDeviceProfile Resolve(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--device", StringComparison.OrdinalIgnoreCase))
                continue;
            if (i + 1 < args.Length && TryParse(args[i + 1], out var fromArg))
                return fromArg;
        }

        var env = Environment.GetEnvironmentVariable("BOOKSMOBILE_DEVICE");
        if (TryParse(env, out var fromEnv))
            return fromEnv;

        return Default;
    }

    public static bool TryParse(string? value, out DesktopDeviceProfile profile)
    {
        profile = Default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "flip8":
            case "zflip8":
            case "phone":
                profile = Flip8;
                return true;
            case "flip8-native":
            case "zflip8-native":
                profile = Flip8Native;
                return true;
            case "desktop":
                profile = Default;
                return true;
            default:
                return false;
        }
    }
}
