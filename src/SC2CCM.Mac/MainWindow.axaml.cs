using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SC2CCM.Core;

namespace SC2CCM.Mac;

public partial class MainWindow : Window
{
    private CampaignManager? _manager;
    private readonly Dictionary<Campaign, CampaignPanel> _panels = new();
    private bool _busy;

    // Kolory pól z oryginalnego FormMain.Designer.cs
    private static readonly Dictionary<Campaign, Color> Accents = new()
    {
        [Campaign.WoL] = Color.FromRgb(128, 128, 255),
        [Campaign.HotS] = Color.FromRgb(221, 150, 255),
        [Campaign.LotV] = Color.FromRgb(255, 220, 90),
        [Campaign.NCO] = Color.FromRgb(110, 230, 110),
    };

    public MainWindow()
    {
        InitializeComponent();

        var index = 0;
        foreach (var info in CampaignInfo.All)
        {
            var panel = new CampaignPanel(info, Accents[info.Campaign]);
            Grid.SetColumn(panel, index % 2);
            Grid.SetRow(panel, index / 2);
            CampaignGrid.Children.Add(panel);
            panel.SetClicked += (_, _) => Run(() => SetActiveAsync(panel));
            panel.DeleteClicked += (_, _) => Run(() => DeleteAsync(panel));
            panel.RestoreClicked += (_, _) => Run(() => RestoreAsync(panel));
            _panels[info.Campaign] = panel;
            index++;
        }

        ImportZipButton.Click += (_, _) => Run(ImportZipAsync);
        ImportFolderButton.Click += (_, _) => Run(ImportFolderAsync);
        RefreshButton.Click += (_, _) => Run(() => RefreshAsync());
        OpenFolderButton.Click += (_, _) => OpenInFinder();
        ChangeSc2Button.Click += (_, _) => Run(() => ChooseInstallAsync());
        InfoButton.Click += (_, _) => Run(ShowInfoAsync);

        AddHandler(DragDrop.DragEnterEvent, OnDragOver);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, (object? _, DragEventArgs _) => DropOverlay.IsVisible = false);
        AddHandler(DragDrop.DropEvent, OnDrop);

        Opened += (_, _) => Run(StartupAsync);
    }

    // ------------------------------------------------------------------ infrastruktura

    /// <summary>Uruchamia akcję z UI i łapie wyjątki, żeby nie wywalić aplikacji.</summary>
    private async void Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            await MessageDialog.InfoAsync(this, "Something went wrong:\n\n" + ex.Message);
        }
    }

    private void Log(string message)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Log(message));
            return;
        }
        LogBox.Text = (LogBox.Text ?? "") + message + "\n";
        LogBox.CaretIndex = LogBox.Text.Length;
    }

    /// <summary>Operacje na plikach w tle (kopiowanie kampanii potrafi trwać), z blokadą UI.</summary>
    private async Task<T> Busy<T>(string text, Func<T> work)
    {
        _busy = true;
        BusyText.Text = text;
        BusyOverlay.IsVisible = true;
        Root.IsEnabled = false;
        try { return await Task.Run(work); }
        finally
        {
            Root.IsEnabled = true;
            BusyOverlay.IsVisible = false;
            _busy = false;
        }
    }

    private Task Busy(string text, Action work) => Busy(text, () => { work(); return true; });

    /// <summary>macOS nie blokuje plików używanych przez grę, więc pytamy wprost.</summary>
    private async Task<bool> ConfirmSc2NotRunningAsync()
    {
        if (!Sc2Locator.IsSc2Running()) return true;
        return await MessageDialog.AskAsync(this,
            "StarCraft II seems to be running.\n\n" +
            "Changing campaign files while you are in a mission or campaign menu can leave the game " +
            "with missing or inconsistent files. It's safest to quit to the main menu (or quit the game) first.\n\n" +
            "Continue anyway?");
    }

    // ------------------------------------------------------------------ start i wybór folderu SC2

    private async Task StartupAsync()
    {
        var path = Sc2Locator.TryLocate();
        if (path == null)
        {
            await MessageDialog.InfoAsync(this,
                "It looks like StarCraft II isn't in the default spot (/Applications/StarCraft II).\n\n" +
                "Please select your StarCraft II folder (the one that contains \"StarCraft II.app\").");
            if (!await ChooseInstallAsync())
            {
                Close();
                return;
            }
            return;
        }
        await UseInstallAsync(path);
    }

    private async Task<bool> ChooseInstallAsync()
    {
        while (true)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select your StarCraft II folder",
                AllowMultiple = false,
                SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri("file:///Applications/")),
            });
            var picked = folders.FirstOrDefault()?.TryGetLocalPath();
            if (picked == null) return false;

            var basePath = Sc2Locator.NormalizeSelection(picked);
            if (Sc2Locator.IsValidInstall(basePath))
            {
                Sc2Locator.Save(basePath);
                await UseInstallAsync(basePath);
                return true;
            }

            var retry = await MessageDialog.AskAsync(this,
                $"\"{basePath}\" doesn't look like a StarCraft II installation " +
                "(no \"StarCraft II.app\" or \"Versions\" folder inside).\n\nTry another folder?");
            if (!retry) return false;
        }
    }

    private async Task UseInstallAsync(string basePath)
    {
        var manager = new CampaignManager(basePath);
        manager.Log += Log;
        _manager = manager;
        PathText.Text = basePath;
        try
        {
            await RefreshAsync();
            Log("Loaded SC2CCM.");
        }
        catch (UnauthorizedAccessException)
        {
            await MessageDialog.InfoAsync(this,
                $"SC2CCM doesn't have permission to write to:\n{basePath}\n\n" +
                "Make sure your macOS user can modify the StarCraft II folder " +
                "(Finder → Get Info → Sharing & Permissions).");
        }
    }

    // ------------------------------------------------------------------ odświeżanie

    private async Task RefreshAsync(IReadOnlyList<string>? justImported = null, bool reload = true)
    {
        var manager = _manager;
        if (manager == null) return;

        var (mods, active) = await Busy("Loading campaigns…", () =>
        {
            if (reload) manager.Reload();
            var m = manager.LoadMods();
            var a = CampaignInfo.All.ToDictionary(i => i.Campaign, i => manager.GetActiveMod(i.Campaign));
            return (m, a);
        });

        foreach (var (campaign, panel) in _panels)
        {
            panel.SetMods(mods[campaign]);
            panel.ShowActive(active[campaign]);
        }

        // Jak w oryginale: po imporcie pojedynczego moda pytamy, czy go aktywować.
        if (justImported is { Count: 1 })
        {
            var name = justImported[0];
            var mod = mods.Where(kv => kv.Key != Campaign.None)
                .SelectMany(kv => kv.Value)
                .FirstOrDefault(m => string.Equals(Path.GetFileName(m.RootFolder), name, StringComparison.OrdinalIgnoreCase));
            if (mod != null)
            {
                var panel = _panels[mod.Campaign];
                panel.Select(mod);
                if (await MessageDialog.AskAsync(this,
                        $"Import of {mod.Title} successful, would you like to make it the active campaign?"))
                {
                    await SetActiveAsync(panel);
                }
            }
        }
    }

    private async Task RefreshActiveAsync()
    {
        var manager = _manager;
        if (manager == null) return;
        foreach (var (campaign, panel) in _panels)
            panel.ShowActive(manager.GetActiveMod(campaign));
        await Task.CompletedTask;
    }

    // ------------------------------------------------------------------ akcje kampanii

    private async Task SetActiveAsync(CampaignPanel panel)
    {
        if (_manager == null || panel.SelectedMod is not { } mod) return;
        if (!await ConfirmSc2NotRunningAsync()) return;

        var manager = _manager;
        var ok = await Busy($"Installing {mod.Title}…", () => manager.SetActive(mod));
        panel.ShowWarning = !ok;
        await RefreshActiveAsync();
    }

    private async Task RestoreAsync(CampaignPanel panel)
    {
        if (_manager == null) return;
        if (!await ConfirmSc2NotRunningAsync()) return;

        var manager = _manager;
        var ok = await Busy($"Restoring {panel.Info.DisplayName}…", () => manager.Restore(panel.Campaign));
        panel.ShowWarning = !ok;
        await RefreshActiveAsync();
    }

    private async Task DeleteAsync(CampaignPanel panel)
    {
        if (_manager == null || panel.SelectedMod is not { } mod) return;
        if (!await MessageDialog.AskAsync(this, $"Are you sure you want to delete {mod.Title}?")) return;

        var manager = _manager;
        await Busy($"Deleting {mod.Title}…", () => manager.DeleteMod(mod));
        await RefreshAsync(reload: false);
    }

    // ------------------------------------------------------------------ import

    private async Task ImportAsync(IReadOnlyList<string> paths)
    {
        if (_manager == null || paths.Count == 0) return;
        var manager = _manager;
        var imported = await Busy("Importing…", () => manager.Import(paths));
        await RefreshAsync(imported, reload: false);
    }

    private async Task ImportZipAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select campaign .zip files to import",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Zip archives") { Patterns = new[] { "*.zip" }, AppleUniformTypeIdentifiers = new[] { "public.zip-archive" } },
            },
        });
        await ImportAsync(files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList());
    }

    private async Task ImportFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select unpacked campaign folders to import",
            AllowMultiple = true,
        });
        await ImportAsync(folders.Select(f => f.TryGetLocalPath()).OfType<string>().ToList());
    }

#pragma warning disable CS0618 // e.Data / GetFiles – w 11.3 nadal działa, nowsze API dopiero w 12.x
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var accept = !_busy && _manager != null && e.Data.Contains(DataFormats.Files);
        e.DragEffects = accept ? DragDropEffects.Copy : DragDropEffects.None;
        DropOverlay.IsVisible = accept;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;
        if (_busy || _manager == null) return;
        var paths = e.Data.GetFiles()?
            .Select(f => f.TryGetLocalPath())
            .OfType<string>()
            .ToList() ?? new List<string>();
        Run(() => ImportAsync(paths));
    }
#pragma warning restore CS0618

    // ------------------------------------------------------------------ pozostałe

    private void OpenInFinder()
    {
        if (_manager == null) return;
        try
        {
            Directory.CreateDirectory(_manager.CustomCampaignsDir);
            var psi = new ProcessStartInfo("open") { UseShellExecute = false };
            psi.ArgumentList.Add(_manager.CustomCampaignsDir);
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log("ERROR: Could not open Finder: " + ex.Message);
        }
    }

    private Task ShowInfoAsync()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        var plus = version.IndexOf('+');
        if (plus >= 0) version = version[..plus];

        return MessageDialog.InfoAsync(this,
            "To install a Custom Campaign:\n" +
            "1. Drag and drop the mod .zip file (or unpacked folder) onto the Custom Campaign Manager, " +
            "or use \"Import .zip…\".\n" +
            "2. Select the custom campaign from the dropdown list and hit \"Set Active\".\n\n" +
            "Mods live in: StarCraft II/Maps/CustomCampaigns\n" +
            "Dependencies (.SC2Mod) are moved to: StarCraft II/Mods\n\n" +
            $"SC2CCM for macOS {version}\n" +
            "Based on SC2CCM — Copyright (c) 2022 by 7thAce and GiantGrantGames. MIT License.");
    }
}
