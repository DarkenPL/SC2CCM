using System.Diagnostics;

namespace SC2CCM.Core;

/// <summary>
/// Znajduje folder instalacji StarCrafta II na macOS i zapamiętuje go.
/// Na Windowsie oryginał czytał rejestr; na Macu sprawdzamy typowe lokalizacje,
/// a jeśli ich nie ma, interfejs prosi użytkownika o wskazanie folderu.
/// </summary>
public static class Sc2Locator
{
    public static string ConfigDirectory
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return OperatingSystem.IsMacOS()
                ? Path.Combine(home, "Library", "Application Support", "SC2CCM")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SC2CCM");
        }
    }

    public static string ConfigFile => Path.Combine(ConfigDirectory, "sc2path.txt");

    public static IEnumerable<string> DefaultCandidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return "/Applications/StarCraft II";
        yield return Path.Combine(home, "Applications", "StarCraft II");
    }

    /// <summary>Czy wskazany folder wygląda na instalację SC2.</summary>
    public static bool IsValidInstall(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return false;
        return Directory.Exists(Path.Combine(dir, "StarCraft II.app"))
            || Directory.Exists(Path.Combine(dir, "Versions"))
            || File.Exists(Path.Combine(dir, ".build.info"))
            || File.Exists(Path.Combine(dir, "StarCraft II.exe"));
    }

    /// <summary>
    /// Zamienia to, co wskazał użytkownik, na folder bazowy:
    /// „StarCraft II.app” → folder nadrzędny, „Maps” → folder nadrzędny.
    /// </summary>
    public static string NormalizeSelection(string selected)
    {
        var path = FileOps.Normalize(selected);
        var name = Path.GetFileName(path);
        if (name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Maps", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            path = Path.GetDirectoryName(path) ?? path;
        }
        return path;
    }

    /// <summary>Zwraca zapisany lub wykryty folder, albo null jeśli trzeba zapytać użytkownika.</summary>
    public static string? TryLocate()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var saved = File.ReadLines(ConfigFile).FirstOrDefault()?.Trim();
                if (IsValidInstall(saved)) return saved;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        var found = DefaultCandidates().FirstOrDefault(IsValidInstall);
        if (found != null) Save(found);
        return found;
    }

    public static void Save(string basePath)
    {
        Directory.CreateDirectory(ConfigDirectory);
        File.WriteAllText(ConfigFile, basePath);
    }

    /// <summary>
    /// Na Windowsie gra blokuje pliki, więc oryginał wykrywał „pliki w użyciu” przez błędy kasowania.
    /// macOS niczego nie blokuje, więc sprawdzamy, czy proces gry działa.
    /// </summary>
    public static bool IsSc2Running()
    {
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                string name;
                try { name = p.ProcessName; }
                catch { continue; }
                finally { p.Dispose(); }

                if (name.StartsWith("SC2CCM", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("SC2", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("SC2-", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("SC2_", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("StarCraft II", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }
}
