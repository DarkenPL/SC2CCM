namespace SC2CCM.Core;

/// <summary>Kampanie StarCrafta II. Wartości liczbowe jak w oryginalnym SC2CCM.</summary>
public enum Campaign
{
    None = 0,
    WoL = 1,
    HotS = 2,
    LotV = 3,
    NCO = 4,
}

/// <summary>Stałe opisujące kampanię: nazwy i folder docelowy w Maps/Campaign.</summary>
public sealed record CampaignInfo(
    Campaign Campaign,
    string DisplayName,
    string ShortName,
    string LogName,
    string[] TargetSubPath)
{
    public static readonly IReadOnlyList<CampaignInfo> All = new[]
    {
        new CampaignInfo(Campaign.WoL,  "Wings of Liberty",   "WoL",  "Wings", Array.Empty<string>()),
        new CampaignInfo(Campaign.HotS, "Heart of the Swarm", "HotS", "Swarm", new[] { "swarm" }),
        new CampaignInfo(Campaign.LotV, "Legacy of the Void", "LotV", "Void",  new[] { "void" }),
        new CampaignInfo(Campaign.NCO,  "Nova Covert Ops",    "NCO",  "Nova",  new[] { "nova" }),
    };

    public static CampaignInfo Get(Campaign c) =>
        All.FirstOrDefault(i => i.Campaign == c)
        ?? throw new ArgumentOutOfRangeException(nameof(c), c, "Brak folderu dla tej kampanii.");
}
