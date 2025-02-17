using StarControl.Api;
using StarControl.Config;
using StardewValley.Menus;

namespace StarControl.UI;

internal static class ConfigurationMenu
{
    public static void Open(
        IModHelper helper,
        ModConfig config,
        PageRegistry pageRegistry,
        bool asRoot = false,
        Action? onClose = null
    )
    {
        // Proactive remove no-longer-installed mods from the actual configuration so that they
        // don't get stuck in the UI and become unable to be removed.
        var activeClientMods = pageRegistry.Mods.Select(mod => mod.UniqueID).ToHashSet();
        config.Integrations.Priorities.RemoveAll(p => !activeClientMods.Contains(p.ModId));
        var items = new ItemsConfigurationViewModel();
        items.ApiItems = pageRegistry
            .StandaloneItems.Select(x => ApiItemViewModel.FromItem(x.Mod, x.Item))
            .ToList();
        var context = new ConfigurationViewModel(helper, config, items);
        context.Controller = asRoot
            ? ViewEngine.OpenRootMenu("Configuration", context)
            : ViewEngine.OpenChildMenu("Configuration", context);
        context.Controller.CanClose = () => context.IsNavigationEnabled;
        context.Controller.CloseAction = () =>
        {
            if (!context.Dismissed && context.HasUnsavedChanges())
            {
                context.ShowCloseConfirmation();
            }
            else
            {
                Game1.playSound("bigDeSelect");
                if (Game1.activeClickableMenu is TitleMenu)
                {
                    TitleMenu.subMenu = null;
                }
                else
                {
                    Game1.exitActiveMenu();
                }
                onClose?.Invoke();
                context.Dispose();
            }
        };
        // CloseSound is normally played before CloseAction has a chance to run; in order to
        // suppress the sound only when displaying the confirmation above, we need to suppress
        // it at all times and play it ad-hoc when "really" closing.
        context.Controller.CloseSound = "";
    }
}
