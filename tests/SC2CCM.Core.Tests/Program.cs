// Prosty test dymny bez frameworka testowego (żeby nie potrzebował NuGeta).
// Uruchom: dotnet run --project tests/SC2CCM.Core.Tests
using System.IO.Compression;
using System.Text;
using SC2CCM.Core;

var failures = 0;
void Check(bool cond, string what)
{
    Console.WriteLine($"{(cond ? "PASS" : "FAIL")}  {what}");
    if (!cond) failures++;
}

var root = Path.Combine(Path.GetTempPath(), "sc2ccm-test-" + Guid.NewGuid().ToString("N")[..8]);
var sc2 = Path.Combine(root, "StarCraft II");
Directory.CreateDirectory(Path.Combine(sc2, "StarCraft II.app", "Contents"));
var downloads = Path.Combine(root, "Downloads");
Directory.CreateDirectory(downloads);

try
{
    Check(Sc2Locator.IsValidInstall(sc2), "folder z „StarCraft II.app” rozpoznany jako instalacja");
    Check(Sc2Locator.NormalizeSelection(Path.Combine(sc2, "StarCraft II.app")) == sc2, "wybór .app → folder nadrzędny");
    Check(!Sc2Locator.IsValidInstall(downloads), "przypadkowy folder odrzucony");

    // Zip „z Windowsa”: separatory '\', CRLF, BOM, zależność .SC2Mod, prolog LotV.
    var winZip = Path.Combine(downloads, "CoolLotV.zip");
    using (var z = ZipFile.Open(winZip, ZipArchiveMode.Create))
    {
        void Add(string name, string content, bool bom = false)
        {
            var e = z.CreateEntry(name);
            using var s = new StreamWriter(e.Open(), new UTF8Encoding(bom));
            s.Write(content);
        }
        Add(@"CoolLotV\Campaign\metadata.txt", "Title=Cool LotV\r\nAuthor=Someone\r\nVersion=1.2\r\nCampaign=Legacy of the Void\r\n", bom: true);
        Add(@"CoolLotV\Campaign\VoidMission01.SC2Map", "map");
        Add(@"CoolLotV\Campaign\lotvprologue\Prologue01.SC2Map", "prologue");
        Add(@"CoolLotV\Dependencies\CoolLotV.SC2Mod", "dep");
    }

    // Zip „z Maca”: śmieci __MACOSX i ._ które nie mogą trafić do gry.
    var macZip = Path.Combine(downloads, "WingsRemix.zip");
    using (var z = ZipFile.Open(macZip, ZipArchiveMode.Create))
    {
        void Add(string name, string content)
        {
            using var s = new StreamWriter(z.CreateEntry(name).Open());
            s.Write(content);
        }
        Add("WingsRemix/metadata.txt", "title=Wings Remix\nauthor=Me\nversion=0.9\ncampaign=wol\n");
        Add("WingsRemix/Liberty01.SC2Map", "map");
        Add("WingsRemix/Assets.SC2Mod/ComponentList.SC2Components", "folder-mod");
        Add("__MACOSX/WingsRemix/._metadata.txt", "junk");
        Add("WingsRemix/.DS_Store", "junk");
    }

    // Folder (nie zip) z modem HotS.
    var hotsFolder = Path.Combine(downloads, "SwarmPlus");
    Directory.CreateDirectory(Path.Combine(hotsFolder, "evolution"));
    File.WriteAllText(Path.Combine(hotsFolder, "metadata.txt"), "Title=Swarm Plus\nCampaign=HotS\nVersion=3\nAuthor=X");
    File.WriteAllText(Path.Combine(hotsFolder, "Zerg01.SC2Map"), "map");
    File.WriteAllText(Path.Combine(hotsFolder, "evolution", "Evo01.SC2Map"), "evo");

    var logs = new List<string>();
    var mgr = new CampaignManager(sc2);
    mgr.Log += m => { logs.Add(m); Console.WriteLine("      log: " + m); };
    mgr.Reload();

    var imported = mgr.Import(new[] { winZip, macZip, hotsFolder, Path.Combine(downloads, "nope.txt") });
    Check(imported.SequenceEqual(new[] { "CoolLotV", "WingsRemix", "SwarmPlus" }), "import zwraca nazwy folderów");
    Check(Directory.Exists(hotsFolder), "folder źródłowy NIE został skasowany");
    Check(!Directory.EnumerateFiles(mgr.CustomCampaignsDir, "*.zip").Any(), "zipy usunięte po rozpakowaniu");
    Check(!Directory.EnumerateFileSystemEntries(sc2, "*", SearchOption.AllDirectories).Any(p => p.Contains('\\')),
          "brak plików z '\\' w nazwie");
    Check(!Directory.Exists(Path.Combine(mgr.CustomCampaignsDir, "WingsRemix", "__MACOSX")), "__MACOSX pominięty");
    Check(!File.Exists(Path.Combine(mgr.CustomCampaignsDir, "WingsRemix", "WingsRemix", ".DS_Store")), ".DS_Store pominięty");
    Check(File.Exists(Path.Combine(mgr.ModsDir, "CoolLotV.SC2Mod")), "plik .SC2Mod przeniesiony do Mods");
    Check(Directory.Exists(Path.Combine(mgr.ModsDir, "Assets.SC2Mod")), "folder .SC2Mod przeniesiony do Mods");
    Check(File.Exists(Path.Combine(mgr.VoidPrologueDir, "Prologue01.SC2Map")), "prolog LotV scalony z voidprologue");

    var mods = mgr.LoadMods();
    Check(mods[Campaign.LotV].Count == 1 && mods[Campaign.LotV][0].Title == "Cool LotV", "LotV: metadata z BOM/CRLF");
    Check(mods[Campaign.WoL].Count == 1 && mods[Campaign.WoL][0].Version == "0.9", "WoL: klucze małymi literami");
    Check(mods[Campaign.HotS].Count == 1, "HotS z folderu");

    // Ustaw wszystkie trzy.
    Check(mgr.SetActive(mods[Campaign.HotS][0]), "Set HotS");
    Check(File.Exists(Path.Combine(mgr.EvolutionDir, "Evo01.SC2Map")), "misje ewolucji skopiowane");
    Check(mgr.SetActive(mods[Campaign.LotV][0]), "Set LotV");
    Check(mgr.SetActive(mods[Campaign.WoL][0]), "Set WoL");
    Check(mgr.GetActiveMod(Campaign.WoL)?.Title == "Wings Remix", "aktywny WoL odczytany");
    Check(mgr.GetActiveMod(Campaign.HotS)?.Title == "Swarm Plus", "ustawienie WoL nie skasowało HotS");
    Check(mgr.GetActiveMod(Campaign.LotV)?.Title == "Cool LotV", "ustawienie WoL nie skasowało LotV");
    Check(File.Exists(Path.Combine(mgr.VoidPrologueDir, "Prologue01.SC2Map")), "ustawienie WoL nie skasowało voidprologue");

    // Przywróć HotS – również ewolucja ma zniknąć, a inne kampanie zostać.
    Check(mgr.Restore(Campaign.HotS), "Restore HotS");
    Check(mgr.GetActiveMod(Campaign.HotS) == null, "HotS wraca do domyślnej");
    Check(!Directory.EnumerateFileSystemEntries(mgr.EvolutionDir).Any(), "ewolucja wyczyszczona");
    Check(Directory.Exists(mgr.EvolutionDir), "folder ewolucji zostaje");
    Check(mgr.GetActiveMod(Campaign.WoL) != null, "Restore HotS nie ruszył WoL");

    // Restore WoL nie może skasować folderów pozostałych kampanii.
    Check(mgr.Restore(Campaign.WoL), "Restore WoL");
    Check(mgr.GetActiveMod(Campaign.LotV)?.Title == "Cool LotV", "Restore WoL nie ruszył LotV");

    // Usuwanie modu.
    var wol = mods[Campaign.WoL][0];
    Check(mgr.DeleteMod(wol), "Delete WoL mod");
    Check(!Directory.Exists(wol.RootFolder), "folder modu usunięty");
    Check(mgr.LoadMods()[Campaign.WoL].Count == 0, "lista WoL pusta po usunięciu");
    var bogus = new Mod { Title = "Bogus", RootFolder = sc2 };
    Check(!mgr.DeleteMod(bogus), "bezpiecznik: nie kasuje folderu spoza CustomCampaigns");
    Check(Directory.Exists(sc2), "instalacja SC2 nietknięta");

    // Folder bez metadata.txt
    Directory.CreateDirectory(Path.Combine(mgr.CustomCampaignsDir, "Broken"));
    mgr.LoadMods();
    Check(logs.Any(l => l.Contains("Unable to find metadata.txt for: Broken")), "komunikat o braku metadata.txt");
}
finally
{
    Directory.Delete(root, true);
}

Console.WriteLine(failures == 0 ? "\nALL PASSED" : $"\n{failures} FAILED");
return failures == 0 ? 0 : 1;
