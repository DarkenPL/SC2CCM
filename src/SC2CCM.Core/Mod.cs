namespace SC2CCM.Core;

public sealed class Mod
{
    public string Title { get; set; } = "N/A";
    public string Author { get; set; } = "N/A";
    public string Description { get; set; } = "N/A";
    public string Version { get; set; } = "N/A";
    public Campaign Campaign { get; set; } = Campaign.None;

    /// <summary>Folder zawierający metadata.txt – jego zawartość jest kopiowana do gry.</summary>
    public string Path { get; set; } = "";

    /// <summary>Folder najwyższego poziomu w CustomCampaigns (usuwany przy „Delete Mod Files”).</summary>
    public string RootFolder { get; set; } = "";

    /// <summary>Ta sama heurystyka co w oryginale (Mod.SetCampaign).</summary>
    public void SetCampaign(string value)
    {
        var c = value.ToLowerInvariant();
        if (c.Contains("wings") || c.Contains("liberty") || c.Contains("wol")) Campaign = Campaign.WoL;
        else if (c.Contains("heart") || c.Contains("swarm") || c.Contains("hots")) Campaign = Campaign.HotS;
        else if (c.Contains("legacy") || c.Contains("void") || c.Contains("lotv")) Campaign = Campaign.LotV;
        else if (c.Contains("nova") || c.Contains("covert") || c.Contains("ops") || c.Contains("nco")) Campaign = Campaign.NCO;
        else Campaign = Campaign.None;
    }

    public override string ToString() => $"{Title} {Version}";
}

public static class MetadataParser
{
    public static Mod Parse(string metadataFile)
    {
        var mod = new Mod();
        // ReadLines radzi sobie z CRLF i BOM z plików tworzonych na Windowsie.
        foreach (var line in File.ReadLines(metadataFile))
            ProcessLine(line, mod);
        return mod;
    }

    public static void ProcessLine(string line, Mod mod)
    {
        var parts = line.Split('=', 2);
        if (parts.Length < 2) return;
        var key = parts[0].Trim().ToLowerInvariant();
        var value = parts[1].Trim();
        switch (key)
        {
            case "title": mod.Title = value; break;
            case "desc": mod.Description = value; break;
            case "campaign": mod.SetCampaign(value); break;
            case "version": mod.Version = value; break;
            case "author": mod.Author = value; break;
        }
    }
}
