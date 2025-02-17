using System.ComponentModel;
using Microsoft.Xna.Framework.Graphics;
using PropertyChanged.SourceGenerator;
using StarControl.Config;

namespace StarControl.UI;

internal partial class ConfigurationViewModel : IDisposable
{
    public static event EventHandler<EventArgs>? Saved;

    public IMenuController? Controller { get; set; }
    public DebugSettingsViewModel Debug { get; } = new();
    public bool Dismissed { get; set; }
    public InputConfigurationViewModel Input { get; } = new();
    public bool IsNavigationDisabled => !IsNavigationEnabled;
    public ItemsConfigurationViewModel Items { get; }
    public ModIntegrationsViewModel Mods { get; }
    public PagerViewModel<NavPageViewModel> Pager { get; } = new();
    public RadialMenuPreview Preview { get; }

    public SoundSettingsViewModel Sound { get; } = new();
    public StyleConfigurationViewModel Style { get; } = new();

    [Notify]
    private Vector2 contentPanelSize;

    [Notify]
    private bool isNavigationEnabled = true;

    [Notify]
    private bool isPreviewVisible;

    private readonly ModConfig config;
    private readonly IModHelper helper;

    private int loadingFrameCount;
    private int loadingPageIndex = 1;

    public ConfigurationViewModel(
        IModHelper helper,
        ModConfig config,
        ItemsConfigurationViewModel items
    )
    {
        this.helper = helper;
        this.config = config;
        Items = items;
        var modId = helper.ModContent.ModID;
        var selfPriority = ModPriorityViewModel.Self(modId, Items);
        Mods = new(helper.ModRegistry, selfPriority);
        Pager.Pages =
        [
            new(
                NavPage.Controls,
                I18n.Config_Tab_Controls_Title(),
                $"Mods/{modId}/Views/Controls",
                autoLoad: true
            ),
            new(NavPage.Style, I18n.Config_Tab_Style_Title(), $"Mods/{modId}/Views/Style"),
            new(NavPage.Actions, I18n.Config_Tab_Actions_Title(), $"Mods/{modId}/Views/Actions"),
            new(NavPage.Sound, I18n.Config_Tab_Sound_Title(), $"Mods/{modId}/Views/Sound"),
            new(NavPage.Mods, I18n.Config_Tab_Mods_Title(), $"Mods/{modId}/Views/ModIntegrations"),
            new(NavPage.Debug, I18n.Config_Tab_Debug_Title(), $"Mods/{modId}/Views/Debug"),
        ];
        Pager.PropertyChanged += Pager_PropertyChanged;
        Items.PropertyChanged += Items_PropertyChanged;
        Preview = new(Style, 500, 500);
        Load(config);
    }

    public bool CancelBlockingAction()
    {
        if (Items.IsReordering)
        {
            return Items.EndReordering();
        }
        return false;
    }

    public void Dispose()
    {
        Preview.Dispose();
        GC.SuppressFinalize(this);
    }

    public bool HandleButtonPress(SButton button)
    {
        if (!IsNavigationEnabled && IsCancelButton(button))
        {
            return CancelBlockingAction();
        }
        return Pager.HandleButtonPress(button);
    }

    public bool HasUnsavedChanges()
    {
        var dummyConfig = new ModConfig();
        SaveSections(dummyConfig);
        return !dummyConfig.Equals(config);
    }

    public void PerformAction(ConfigurationAction action)
    {
        switch (action)
        {
            case ConfigurationAction.Save:
                Save(config);
                Dismissed = true;
                Controller?.Close();
                break;
            case ConfigurationAction.Cancel:
                Dismissed = true;
                Controller?.Close();
                break;
            case ConfigurationAction.Reset:
                Game1.playSound("drumkit6");
                Load(new ModConfig());
                break;
            default:
                throw new ArgumentException($"Unsupported menu action: {action}");
        }
    }

    public void Save(ModConfig config)
    {
        SaveSections(config);
        helper.WriteConfig(config);
        Saved?.Invoke(this, EventArgs.Empty);
    }

    public void ShowCloseConfirmation()
    {
        var portrait =
            Game1.getCharacterFromName("Krobus")?.Portrait
            ?? Game1.content.Load<Texture2D>("Portraits\\Krobus");
        var context = new ConfirmationViewModel()
        {
            DialogTitle = I18n.Confirmation_Config_Title(),
            DialogDescription = I18n.Confirmation_Config_Description(),
            SaveTitle = I18n.Confirmation_Config_Save_Title(),
            SaveDescription = I18n.Confirmation_Config_Save_Description(),
            RevertTitle = I18n.Confirmation_Config_Revert_Title(),
            RevertDescription = I18n.Confirmation_Config_Revert_Description(),
            CancelTitle = I18n.Confirmation_Config_Cancel_Title(),
            CancelDescription = I18n.Confirmation_Config_Cancel_Description(),
            Sprite = new(portrait, Game1.getSourceRectForStandardTileSheet(portrait, 3)),
        };
        var confirmationController = ViewEngine.OpenChildMenu("Confirmation", context);
        confirmationController.CloseOnOutsideClick = true;
        context.Close = confirmationController.Close;
        confirmationController.Closed += () => ConfirmClose(context.Result);
    }

    public void Update()
    {
        if (loadingPageIndex >= Pager.Pages.Count)
        {
            return;
        }
        loadingFrameCount = (loadingFrameCount + 1) % 3;
        if (loadingFrameCount > 0)
        {
            return;
        }
        Pager.Pages[loadingPageIndex].Loaded = true;
        loadingPageIndex++;
    }

    private void ConfirmClose(ConfirmationResult result)
    {
        switch (result)
        {
            case ConfirmationResult.Yes:
                PerformAction(ConfigurationAction.Save);
                break;
            case ConfirmationResult.No:
                PerformAction(ConfigurationAction.Cancel);
                break;
        }
    }

    private static bool IsCancelButton(SButton button)
    {
        return button is SButton.ControllerB or SButton.ControllerBack
            || (
                button.TryGetStardewInput(out var inputButton)
                && Game1.options.menuButton.Contains(inputButton)
            );
    }

    private void Items_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemsConfigurationViewModel.GrabbedItem))
        {
            IsNavigationEnabled = Items.GrabbedItem is null;
            if (Items.GrabbedItem is { } item)
            {
                Controller?.SetCursorAttachment(
                    item.Icon.Texture,
                    item.Icon.SourceRect,
                    new(64 * item.Icon.SourceRect.Width / item.Icon.SourceRect.Height, 64)
                );
            }
            else
            {
                Controller?.ClearCursorAttachment();
            }
        }
    }

    private void Load(ModConfig config)
    {
        Input.Load(config.Input);
        Style.Load(config.Style);
        Items.Load(config.Items);
        Sound.Load(config.Sound);
        Mods.Load(config.Integrations);
        Debug.Load(config.Debug);
    }

    private void OnContentPanelSizeChanged()
    {
        // Explicitly assigning it here, as opposed to passing in a selector to the constructor, guarantees that the
        // property change event will fire for the dependent models as well.
        Pager.ContentPanelSize = contentPanelSize;
        Items.Pager.ContentPanelSize = ContentPanelSize;
    }

    private void Pager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Pager.SelectedPageIndex))
        {
            IsPreviewVisible = Pager.SelectedPageIndex == 1; // Styles
        }
    }

    private void SaveSections(ModConfig config)
    {
        Input.Save(config.Input);
        Style.Save(config.Style);
        Items.Save(config.Items);
        Sound.Save(config.Sound);
        Mods.Save(config.Integrations);
        Debug.Save(config.Debug);
    }
}

internal enum NavPage
{
    Controls,
    Style,
    Actions,
    Sound,
    Mods,
    Debug,
}

internal partial class NavPageViewModel(
    NavPage id,
    string title,
    string pageAssetName,
    bool autoLoad = false
) : PageViewModel((int)id)
{
    public NavPage Id { get; } = id;
    public string PageAssetName { get; } = pageAssetName;
    public string Title { get; } = title;

    [Notify]
    private bool loaded = autoLoad;
}
