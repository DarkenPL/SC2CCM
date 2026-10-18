namespace SC2CCM.Core;

internal static class FileOps
{
    /// <summary>Wyszukiwanie bez rozróżniania wielkości liter (na Unixie .NET domyślnie rozróżnia).</summary>
    public static readonly EnumerationOptions Recursive = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = 0,
    };

    public static readonly EnumerationOptions TopLevel = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = 0,
    };

    public static bool IsJunkName(string name) =>
        name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase)
        || name.Equals("__MACOSX", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("._", StringComparison.Ordinal);

    /// <summary>Rekurencyjna kopia z nadpisywaniem (odpowiednik copyFilesAndFolders).</summary>
    /// <param name="excludeDir">Opcjonalnie: foldery (wraz z zawartością), których nie kopiować.</param>
    public static void CopyDirectory(string source, string target, Func<string, bool>? excludeDir = null)
    {
        Directory.CreateDirectory(target);
        foreach (var dir in Directory.EnumerateDirectories(source, "*", Recursive))
        {
            if (PathHasJunk(source, dir) || IsExcluded(source, dir, excludeDir)) continue;
            Directory.CreateDirectory(System.IO.Path.Combine(target, System.IO.Path.GetRelativePath(source, dir)));
        }
        foreach (var file in Directory.EnumerateFiles(source, "*", Recursive))
        {
            if (PathHasJunk(source, file)
                || IsExcluded(source, System.IO.Path.GetDirectoryName(file)!, excludeDir)) continue;
            var dest = System.IO.Path.Combine(target, System.IO.Path.GetRelativePath(source, file));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    /// <summary>Czy folder (lub któryś z jego folderów nadrzędnych aż do root) jest wykluczony.</summary>
    private static bool IsExcluded(string root, string dir, Func<string, bool>? excludeDir)
    {
        if (excludeDir == null) return false;
        var current = Normalize(dir);
        var stop = Normalize(root);
        while (current.Length > stop.Length)
        {
            if (excludeDir(current)) return true;
            current = System.IO.Path.GetDirectoryName(current) ?? stop;
        }
        return false;
    }

    private static bool PathHasJunk(string root, string path) =>
        System.IO.Path.GetRelativePath(root, path)
            .Split(System.IO.Path.DirectorySeparatorChar)
            .Any(IsJunkName);

    public static string Normalize(string path) =>
        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));

    public static bool SamePath(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b),
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}
