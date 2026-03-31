namespace SnapLingoWindows.Services;

public static class AppPaths
{
    public static string RootDirectory
    {
        get
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SnapLingoWindows"
            );
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string SecretDirectory
    {
        get
        {
            var path = Path.Combine(RootDirectory, "Secrets");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
