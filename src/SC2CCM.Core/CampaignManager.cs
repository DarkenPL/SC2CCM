namespace SC2CCM.Core;

/// <summary>
/// Cała logika z FormMain.cs oryginału, bez interfejsu i bez założeń windowsowych.
/// Komunikaty logu są po angielsku, tak jak w oryginalnej aplikacji.
/// </summary>
public sealed class CampaignManager
{
    public string BasePath { get; }
    public string MapsDir => Path.Combine(BasePath, "Maps");
    public string CampaignDir => Path.Combine(MapsDir, "Campaign");
    public string CustomCampaignsDir => Path.Combine(MapsDir, "CustomCampaigns");
    public string ModsDir => Path.Combine(BasePath, "Mods");
    public string EvolutionDir => Path.Combine(CampaignDir, "swarm", "evolution");
    public string VoidPrologueDir => Path.Combine(CampaignDir, "voidprologue");

    public event Action<string>? Log;

    public CampaignManager(string basePath) => BasePath = FileOps.Normalize(basePath);

    private void Write(string message) => Log?.Invoke(message);

    public string TargetDir(Campaign c)
    {
        var info = CampaignInfo.Get(c);
        return info.TargetSubPath.Aggregate(CampaignDir, Path.Combine);
    }

    /// <summary>Foldery kampanii, których nie wolno usuwać przy czyszczeniu folderu nadrzędnego.</summary>
    private IEnumerable<string> ProtectedDirs()
    {
        yield return TargetDir(Campaign.HotS);
        yield return EvolutionDir;
        yield return TargetDir(Campaign.LotV);
        yield return VoidPrologueDir;
        yield return TargetDir(Campaign.NCO);
    }

    // ------------------------------------------------------------------ start / reload

    /// <summary>Odpowiednik SC2MM_Load: foldery, rozpakowanie zipów, przeniesienie zależności.</summary>
    public void Reload()
    {
        VerifyDirectories();
        UnzipZips();
        HandleDependencies();
    }

    public void VerifyDirectories()
    {
        foreach (var dir in new[]
                 {
                     CampaignDir, CustomCampaignsDir, TargetDir(Campaign.HotS), EvolutionDir,
                     TargetDir(Campaign.LotV), VoidPrologueDir, TargetDir(Campaign.NCO), ModsDir,
                 })
        {
            Directory.CreateDirectory(dir);
        }
    }

    public void UnzipZips()
    {
        foreach (var zip in Directory.EnumerateFiles(CustomCampaignsDir, "*.zip", FileOps.TopLevel).ToList())
        {
            var name = Path.GetFileNameWithoutExtension(zip);
            var target = Path.Combine(CustomCampaignsDir, name);
            try
            {
                ZipService.ExtractSafe(zip, target);
                Write($"Unzipped {name}.");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Write($"ERROR: Could not unzip {name}: {ex.Message}");
                continue;
            }
            // Prolog LotV zostaje w folderze moda – trafia do Maps/Campaign/voidprologue dopiero przy „Set Active”.

            try { File.Delete(zip); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Write($"Could not delete zip file {Path.GetFileName(zip)} - file in use.");
            }
        }
    }

    /// <summary>Przenosi wszystkie pliki i foldery *.SC2Mod do folderu Mods.</summary>
    public void HandleDependencies()
    {
        foreach (var file in Directory.EnumerateFiles(CustomCampaignsDir, "*.SC2Mod", FileOps.Recursive).ToList())
        {
            var name = Path.GetFileName(file);
            var dest = Path.Combine(ModsDir, name);
            try
            {
                if (Directory.Exists(dest)) Directory.Delete(dest, true);
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(file, dest);
                Write($"Moved {name} to Dependencies folder.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Write($"ERROR: Could not replace {name} - exit StarCraft II and hit \"Refresh\" to fix install properly.");
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(CustomCampaignsDir, "*.SC2Mod", FileOps.Recursive).ToList())
        {
            if (!Directory.Exists(dir)) continue; // mógł zostać przeniesiony razem z folderem nadrzędnym
            var name = Path.GetFileName(dir);
            var dest = Path.Combine(ModsDir, name);
            try
            {
                if (File.Exists(dest)) File.Delete(dest);
                if (Directory.Exists(dest)) Directory.Delete(dest, true);
                Directory.Move(dir, dest);
                Write($"Moved {name} to Dependencies folder.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Write($"ERROR: Could not replace {name} - exit StarCraft II and hit \"Refresh\" to fix install properly.");
            }
        }
    }

    // ------------------------------------------------------------------ mods

    /// <summary>Odpowiednik populateModLists – mody pogrupowane wg kampanii.</summary>
    public Dictionary<Campaign, List<Mod>> LoadMods()
    {
        var result = Enum.GetValues<Campaign>().ToDictionary(c => c, _ => new List<Mod>());

        foreach (var dir in Directory.EnumerateDirectories(CustomCampaignsDir, "*", FileOps.TopLevel)
                     .Where(d => !Path.GetFileName(d).StartsWith('.') && !FileOps.IsJunkName(Path.GetFileName(d)))
                     .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var metas = Directory.EnumerateFiles(dir, "metadata.txt", FileOps.Recursive)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}__MACOSX{Path.DirectorySeparatorChar}"))
                .OrderBy(f => f.Length)
                .ToList();

            if (metas.Count == 0)
            {
                Write($"FAILED TO LOAD: Unable to find metadata.txt for: {Path.GetFileName(dir)}");
                continue;
            }
            if (metas.Count > 1)
                Write($"WARNING: Multiple metadata.txt found for: {Path.GetFileName(dir)}");

            var mod = MetadataParser.Parse(metas[0]);
            mod.Path = Path.GetDirectoryName(metas[0])!;
            mod.RootFolder = dir;
            if (mod.Campaign == Campaign.None)
                Write($"WARNING: {mod.Title} does not specify a known campaign in metadata.txt.");
            result[mod.Campaign].Add(mod);
        }

        foreach (var list in result.Values)
            list.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>Aktywny mod danej kampanii albo null (= kampania Blizzarda).</summary>
    public Mod? GetActiveMod(Campaign c)
    {
        var dir = TargetDir(c);
        if (!Directory.Exists(dir)) return null;
        var meta = Directory.EnumerateFiles(dir, "metadata.txt", FileOps.TopLevel).FirstOrDefault();
        return meta == null ? null : MetadataParser.Parse(meta);
    }

    /// <summary>Kasuje pliki w folderze i podfoldery – z wyjątkiem folderów innych kampanii.</summary>
    public bool ClearDir(string dir)
    {
        if (!Directory.Exists(dir))
        {
            Write($"ERROR: Did not find directory {dir} to clear.");
            return true;
        }

        var ok = true;
        foreach (var file in Directory.EnumerateFiles(dir, "*", FileOps.TopLevel).ToList())
        {
            try { File.Delete(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Write($"ERROR: Could not delete campaign file {Path.GetFileName(file)} - please exit the campaign and try again.");
                ok = false;
            }
        }

        var protectedDirs = ProtectedDirs().ToList();
        foreach (var sub in Directory.EnumerateDirectories(dir, "*", FileOps.TopLevel).ToList())
        {
            if (protectedDirs.Any(p => FileOps.SamePath(p, sub))) continue;
            try { Directory.Delete(sub, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Write($"ERROR: Could not delete folder {Path.GetFileName(sub)}: {ex.Message}");
                ok = false;
            }
        }
        return ok;
    }

    private bool ClearCampaign(Campaign c)
    {
        var ok = ClearDir(TargetDir(c));
        if (c == Campaign.HotS) ok &= ClearDir(EvolutionDir);
        if (c == Campaign.LotV) ok &= ClearDir(VoidPrologueDir);
        return ok;
    }

    private static readonly string[] PrologueFolderNames = { "voidprologue", "lotvprologue" };

    /// <summary>Foldery prologu LotV w modzie (paczki używają nazwy „voidprologue” albo „lotvprologue”).</summary>
    private static List<string> FindPrologueDirs(Mod mod)
    {
        var root = string.IsNullOrEmpty(mod.RootFolder) ? mod.Path : mod.RootFolder;
        return Directory.EnumerateDirectories(root, "*", FileOps.Recursive)
            .Where(d => PrologueFolderNames.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>„Set to Active Campaign”.</summary>
    public bool SetActive(Mod mod)
    {
        var info = CampaignInfo.Get(mod.Campaign);
        if (!ClearCampaign(mod.Campaign))
        {
            Write($"ERROR: Could not set {info.LogName} campaign - SC2 files in use.");
            return false;
        }
        try
        {
            if (mod.Campaign == Campaign.LotV)
            {
                // Prolog idzie do Maps/Campaign/voidprologue, reszta do Maps/Campaign/void.
                var prologues = FindPrologueDirs(mod);
                FileOps.CopyDirectory(mod.Path, TargetDir(mod.Campaign),
                    excludeDir: d => prologues.Any(p => FileOps.SamePath(p, d)));
                foreach (var p in prologues)
                    FileOps.CopyDirectory(p, VoidPrologueDir);
                if (prologues.Count > 0)
                    Write("Installed LotV prologue missions.");
            }
            else
            {
                FileOps.CopyDirectory(mod.Path, TargetDir(mod.Campaign));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Write($"ERROR: Could not copy {mod.Title}: {ex.Message}");
            return false;
        }
        Write($"Set {info.LogName} Campaign to {mod.Title}!");
        return true;
    }

    /// <summary>„Restore to Unmodified”.</summary>
    public bool Restore(Campaign c)
    {
        var info = CampaignInfo.Get(c);
        Write($"Resetting {info.LogName} Campaign to default.");
        if (ClearCampaign(c))
        {
            Write("Clear complete!");
            return true;
        }
        Write($"ERROR: Could not reset {info.LogName} campaign - SC2 files in use.");
        return false;
    }

    /// <summary>„Delete Mod Files” – usuwa mod z CustomCampaigns.</summary>
    public bool DeleteMod(Mod mod)
    {
        var root = mod.RootFolder;
        // Bezpiecznik: kasujemy wyłącznie bezpośredni podfolder CustomCampaigns.
        if (string.IsNullOrEmpty(root)
            || !FileOps.SamePath(Path.GetDirectoryName(FileOps.Normalize(root))!, CustomCampaignsDir))
        {
            Write($"ERROR: Refusing to delete {mod.Title} - unexpected location.");
            return false;
        }
        try
        {
            Directory.Delete(root, true);
            Write($"Deleted {mod.Title} from local storage.");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Write($"ERROR: Could not delete {mod.Title} - a file may be open somewhere.");
            return false;
        }
    }

    // ------------------------------------------------------------------ import

    /// <summary>
    /// Kopiuje przeciągnięte/wybrane .zip lub foldery do CustomCampaigns i robi Reload.
    /// Zwraca nazwy folderów, pod którymi mody wylądują (do pytania „ustawić jako aktywny?”).
    /// W odróżnieniu od oryginału folder źródłowy NIE jest kasowany.
    /// </summary>
    public IReadOnlyList<string> Import(IEnumerable<string> paths)
    {
        var imported = new List<string>();
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(path, Path.Combine(CustomCampaignsDir, Path.GetFileName(path)), true);
                    imported.Add(Path.GetFileNameWithoutExtension(path));
                }
                else if (Directory.Exists(path))
                {
                    var name = Path.GetFileName(FileOps.Normalize(path));
                    FileOps.CopyDirectory(path, Path.Combine(CustomCampaignsDir, name));
                    imported.Add(name);
                }
                else
                {
                    Write($"Skipped {Path.GetFileName(path)} - only .zip files and folders can be imported.");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Write($"ERROR: Could not import {Path.GetFileName(path)}: {ex.Message}");
            }
        }
        Reload();
        return imported;
    }
}
