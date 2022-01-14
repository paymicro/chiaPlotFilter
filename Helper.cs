using System.Text.RegularExpressions;

internal class Helper
{
    public static string GetFork()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        var forkArgI = Array.IndexOf(arguments, "-fork");
        if (forkArgI != -1 && arguments.Length > forkArgI)
        {
            return arguments[forkArgI + 1];
        }
        return "chia";
    }

    public static string GetAppPath(string forkName)
    {
        var app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), $"{forkName}-blockchain");
        string[] dirs = Directory.GetDirectories(app, "app-*", SearchOption.TopDirectoryOnly);
        if (dirs.Any())
        {
            app = Path.Combine(dirs.First(), "resources", "app.asar.unpacked", "daemon", $"{forkName}.exe");
        }
        return File.Exists(app) ? app : string.Empty;
    }

    public static string? GetRegexMatch(string pattern, string input)
    {
        return new Regex(pattern).Matches(input).FirstOrDefault()?.Groups["res"].Value;
    }
}