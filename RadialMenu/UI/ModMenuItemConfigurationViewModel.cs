using System.ComponentModel;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PropertyChanged.SourceGenerator;
using RadialMenu.Gmcm;
using StardewModdingAPI.Utilities;
using StardewValley.ItemTypeDefinitions;

namespace RadialMenu.UI;

internal enum ItemIconType
{
    Item,
    Custom,
}

internal enum ItemSyncType
{
    None,
    Gmcm,
}

// TODO: Add function/UI to remove this item.

internal partial class ModMenuItemConfigurationViewModel
{
    public bool CanEditDescription =>
        SyncType.SelectedValue != ItemSyncType.Gmcm || GmcmSync?.EnableDescriptionSync != true;
    public bool CanEditKeybind => SyncType.SelectedValue == ItemSyncType.None;
    public bool CanEditName =>
        SyncType.SelectedValue != ItemSyncType.Gmcm || GmcmSync?.EnableTitleSync != true;
    public string Id { get; }
    public Sprite Icon =>
        (IconType.SelectedValue == ItemIconType.Item ? IconFromItemId : CustomIcon)
        ?? new(Game1.mouseCursors, new(240, 192, 16, 16)); // Question mark
    public EnumSegmentsViewModel<ItemIconType> IconType { get; } = new();
    public bool IsCustomIcon => IconType.SelectedValue == ItemIconType.Custom;
    public bool IsGmcmSyncVisible => SyncType.SelectedValue == ItemSyncType.Gmcm;
    public bool IsStandardIcon => IconType.SelectedValue == ItemIconType.Item;
    public EnumSegmentsViewModel<ItemSyncType> SyncType { get; } = new();

    [Notify]
    private Sprite? customIcon;

    [Notify]
    private string description = "";

    [Notify]
    private GmcmSyncSettingsViewModel? gmcmSync;

    [Notify]
    private Keybind keybind = new();

    [Notify]
    private string iconAssetPath = "";

    [Notify]
    private Sprite? iconFromItemId;

    [Notify]
    private string? iconItemId = null;

    [Notify]
    private Rectangle iconSourceRect = Rectangle.Empty;

    [Notify]
    private string iconSourceRectText = "";

    [Notify]
    private string name = "";

    [Notify]
    private ShelfViewModel<ParsedItemData> searchResults = ShelfViewModel<ParsedItemData>.Empty;

    [Notify]
    private string searchText = "";

    private readonly Task<ParsedItemData[]> allItems;
    private readonly object searchLock = new();

    private CancellationTokenSource searchCancellationTokenSource = new();

    public ModMenuItemConfigurationViewModel(string id, Task<ParsedItemData[]> allItems)
    {
        Id = id;
        SyncType.ValueChanged += SyncType_ValueChanged;
        IconType.ValueChanged += IconType_ValueChanged;
        this.allItems = allItems;
        UpdateRawSearchResults();
    }

    public void OnRandomizeButtonHover()
    {
        Game1.playSound("Cowboy_Footstep");
    }

    public void PickRandomIcon()
    {
        Game1.playSound("smallSelect");
        IconType.SelectedValue = ItemIconType.Item;
        var allItems = this.allItems.Result;
        int index = Random.Shared.Next(allItems.Length);
        IconItemId = allItems[index].QualifiedItemId;
        UpdateRawSearchResults();
    }

    public void SetIconFromSearchResults(Vector2 position)
    {
        SearchResults.ScrollToPoint(position);
        if (SearchResults.SelectedItem?.QualifiedItemId is { } id && id != IconItemId)
        {
            Game1.playSound("smallSelect");
            IconItemId = id;
        }
    }

    private void GmcmSync_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            e.PropertyName
                is nameof(GmcmSyncSettingsViewModel.SelectedMod)
                    or nameof(GmcmSyncSettingsViewModel.EnableTitleSync)
            && GmcmSync?.SelectedMod is not null
            && GmcmSync.EnableTitleSync
        )
        {
            Name = GmcmSync.SelectedMod.Name;
        }
        else if (
            e.PropertyName
                is nameof(GmcmSyncSettingsViewModel.SelectedOption)
                    or nameof(GmcmSyncSettingsViewModel.EnableDescriptionSync)
            && GmcmSync?.SelectedOption is not null
            && GmcmSync.EnableDescriptionSync
        )
        {
            Description = GmcmSync.SelectedOption.SimpleName;
        }

        if (
            e.PropertyName == nameof(GmcmSyncSettingsViewModel.SelectedOption)
            && GmcmSync?.SelectedOption is { } option
        )
        {
            Keybind = option.CurrentKeybind;
        }

        if (e.PropertyName == nameof(GmcmSyncSettingsViewModel.EnableTitleSync))
        {
            OnPropertyChanged(new(nameof(CanEditName)));
        }
        if (e.PropertyName == nameof(GmcmSyncSettingsViewModel.EnableDescriptionSync))
        {
            OnPropertyChanged(new(nameof(CanEditDescription)));
        }
    }

    private async Task<ParsedItemData[]> GetRawSearchResults(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return await this.allItems;
        }
        if (ItemRegistry.IsQualifiedItemId(SearchText))
        {
            var exactItem = ItemRegistry.GetData(SearchText);
            return exactItem is not null ? [exactItem] : [];
        }
        if (int.TryParse(SearchText, out var objectId))
        {
            var exactItem = ItemRegistry.GetData("(O)" + objectId);
            return exactItem is not null ? [exactItem] : [];
        }
        var allItems = await this.allItems;
        var matches = allItems
            .Where(item =>
                !cancellationToken.IsCancellationRequested
                && item.DisplayName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
            )
            .ToArray();
        return !cancellationToken.IsCancellationRequested ? matches : [];
    }

    private void IconType_ValueChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(new(nameof(Icon)));
        OnPropertyChanged(new(nameof(IsCustomIcon)));
        OnPropertyChanged(new(nameof(IsStandardIcon)));
    }

    private void OnIconAssetPathChanged()
    {
        try
        {
            var texture = Game1.content.Load<Texture2D>(IconAssetPath);
            CustomIcon = new(texture, IconSourceRect);
        }
        catch (ContentLoadException)
        {
            CustomIcon = null;
        }
    }

    private void OnIconItemIdChanged()
    {
        if (!string.IsNullOrEmpty(IconItemId))
        {
            var data = ItemRegistry.GetDataOrErrorItem(IconItemId);
            IconFromItemId = new(data.GetTexture(), data.GetSourceRect());
        }
    }

    private void OnIconSourceRectChanged()
    {
        if (CustomIcon is not null)
        {
            CustomIcon = new(CustomIcon.Texture, IconSourceRect);
        }
    }

    private void OnIconSourceRectTextChanged()
    {
        IconSourceRect = Sprite.TryParseRectangle(IconSourceRectText, out var rect)
            ? rect
            : Rectangle.Empty;
    }

    private void OnSearchTextChanged()
    {
        UpdateRawSearchResults();
    }

    private void SyncType_ValueChanged(object? sender, EventArgs e)
    {
        if (
            SyncType.SelectedValue == ItemSyncType.Gmcm
            && GenericModConfigKeybindings.Instance is not null
        )
        {
            GmcmSync ??= new(GenericModConfigKeybindings.Instance);
            GmcmSync.PropertyChanged += GmcmSync_PropertyChanged;
        }
        OnPropertyChanged(new(nameof(IsGmcmSyncVisible)));
        OnPropertyChanged(new(nameof(CanEditName)));
        OnPropertyChanged(new(nameof(CanEditDescription)));
        OnPropertyChanged(new(nameof(CanEditKeybind)));
    }

    private void UpdateRawSearchResults()
    {
        searchCancellationTokenSource.Cancel();
        searchCancellationTokenSource = new();
        var cancellationToken = searchCancellationTokenSource.Token;
        var searchTask = Task.Run(() => GetRawSearchResults(cancellationToken), cancellationToken);
        searchTask.ContinueWith(
            t =>
            {
                if (t.IsFaulted)
                {
                    Logger.Log($"Failed searching for items: {t.Exception}", LogLevel.Error);
                    return;
                }
                lock (searchLock)
                {
                    var previousIconItemId = IconItemId; // Save for thread safety
                    var selectedIndex = !string.IsNullOrEmpty(previousIconItemId)
                        ? Math.Max(
                            Array.FindIndex(t.Result, r => r.QualifiedItemId == previousIconItemId),
                            0
                        )
                        : 0;
                    SearchResults = new(
                        t.Result,
                        visibleSize: 5,
                        bufferSize: 2,
                        centerMargin: 20,
                        itemDistance: 80,
                        initialSelectedIndex: selectedIndex
                    );
                }
            },
            cancellationToken
        );
    }
}
