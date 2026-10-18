using System.IO.Compression;

namespace SC2CCM.Core;

public static class ZipService
{
    /// <summary>
    /// Rozpakowuje archiwum z zabezpieczeniem przed „zip slip”.
    /// Dodatkowo względem oryginału: zamienia '\' na '/' (archiwa z Windowsa)
    /// i pomija śmieci z macOS (__MACOSX, ._*, .DS_Store).
    /// </summary>
    public static void ExtractSafe(string zipPath, string destination)
    {
        var destRoot = FileOps.Normalize(Directory.CreateDirectory(destination).FullName) + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var relative = entry.FullName.Replace('\\', '/');
            var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || segments.Any(FileOps.IsJunkName)) continue;

            var full = Path.GetFullPath(Path.Combine(destRoot, Path.Combine(segments)));
            if (!full.StartsWith(destRoot, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Archiwum próbuje zapisać plik poza folderem docelowym: {entry.FullName}");

            if (relative.EndsWith('/'))
            {
                Directory.CreateDirectory(full);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            entry.ExtractToFile(full, overwrite: true);
        }
    }
}
