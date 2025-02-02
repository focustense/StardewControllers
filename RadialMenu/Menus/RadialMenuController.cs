using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadialMenu.Config;
using RadialMenu.Graphics;
using RadialMenu.Input;
using StardewValley.Menus;

namespace RadialMenu.Menus;

internal class RadialMenuController(
    IInputHelper inputHelper,
    ModConfig config,
    Farmer player,
    RadialMenuPainter radialMenuPainter,
    QuickSlotController quickSlotController,
    params IRadialMenu[] menus
)
{
    public event EventHandler<ItemActivationEventArgs>? ItemActivated;

    public bool Enabled
    {
        get => enabled;
        set
        {
            if (value == enabled)
            {
                return;
            }
            Logger.Log(LogCategory.Menus, $"Controller enabled -> {value}");
            enabled = value;
            if (!value)
            {
                Reset();
            }
        }
    }
    public bool IsMenuActive => activeMenu is not null;

    private const int MENU_ANIMATION_DURATION_MS = 120;
    private const int QUICK_SLOT_ANIMATION_DURATION_MS = 250;

    private IRadialMenu? activeMenu;
    private float? cursorAngle;
    private PendingActivation? delayedItem;
    private TimeSpan elapsedActivationDelay;
    private bool enabled;
    private float fadeOpacity;
    private int focusedIndex;
    private IRadialMenuItem? focusedItem;
    private float menuOpenTimeMs;
    private float menuScale;
    private float quickSlotOpacity;

    public void Draw(SpriteBatch b, Rectangle? viewport = null)
    {
        if (activeMenu?.GetSelectedPage() is not { } page)
        {
            return;
        }
        viewport ??= Viewports.DefaultViewport;
        b.Draw(Game1.fadeToBlackRect, viewport.Value, null, Color.Black * fadeOpacity);
        // Forcing a new sprite batch appears to be the only way to get the menu, which includes a
        // BasicEffect, to draw over rather than under the fade.
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        radialMenuPainter.Items = page.Items;
        radialMenuPainter.Scale = menuScale;
        radialMenuPainter.Paint(
            b,
            page.SelectedItemIndex,
            focusedIndex,
            cursorAngle,
            GetSelectionBlend(),
            viewport
        );
        quickSlotController.Draw(b, viewport.Value, quickSlotOpacity);
    }

    public void Invalidate()
    {
        Logger.Log(LogCategory.Menus, "Menu controller invalidated.", LogLevel.Info);
        foreach (var menu in menus)
        {
            menu.Invalidate();
        }
        quickSlotController.Invalidate();
    }

    public void Update(TimeSpan elapsed)
    {
        if (!Enabled)
        {
            return;
        }

        foreach (var menu in menus)
        {
            menu.Toggle.PreUpdate();
        }

        AnimateMenuOpen(elapsed);

        // Used only for animation, doesn't interfere with logic right now.
        quickSlotController.Update(elapsed);

        if (TryActivateDelayedItem(elapsed))
        {
            Logger.Log(LogCategory.Menus, "Delayed item was activated; skipping rest of update.");
            return;
        }

        var previousActiveMenu = activeMenu;
        TryInteractWithActiveMenu();
        foreach (var menu in menus)
        {
            if (menu == previousActiveMenu)
            {
                continue;
            }
            menu.Toggle.Update(allowOn: activeMenu is null);
            if (menu.Toggle.State != MenuToggleState.On)
            {
                continue;
            }
            Logger.Log(
                LogCategory.Menus,
                $"Menu {Array.IndexOf(menus, menu)} became active; "
                    + $"RememberSelection = {config.Input.RememberSelection}."
            );
            Sound.Play(config.Sound.MenuOpenSound);
            if (!config.Input.RememberSelection)
            {
                menu.ResetSelectedPage();
            }
            else if (menu.GetSelectedPage()?.IsEmpty() == true)
            {
                Logger.Log(
                    LogCategory.Menus,
                    "Menu is configured to remember selection, but the selected page is empty. "
                        + "Attempting to navigate to previous page.",
                    LogLevel.Info
                );
                // Will automatically try to find a non-empty page.
                menu.PreviousPage();
            }
            activeMenu = menu;
            if (previousActiveMenu is null && activeMenu is not null)
            {
                AnimateMenuOpen(elapsed); // Skip "zero" frame
            }
        }

        TryActivateQuickSlot();
    }

    private void ActivateFocusedItem()
    {
        if (focusedItem is null)
        {
            return;
        }
        if (
            SuppressIfPressed(config.Input.PrimaryActionButton)
            || CheckStickActivation(secondaryAction: false)
        )
        {
            Logger.Log(LogCategory.Menus, $"Primary activation triggered for {focusedItem.Title}.");
            ActivateItem(focusedItem, secondaryAction: false);
        }
        else if (
            SuppressIfPressed(config.Input.SecondaryActionButton)
            || CheckStickActivation(secondaryAction: true)
        )
        {
            Logger.Log(
                LogCategory.Menus,
                $"Secondary activation triggered for {focusedItem.Title}."
            );
            ActivateItem(focusedItem, secondaryAction: true);
        }
    }

    private ItemActivationResult ActivateItem(
        IRadialMenuItem item,
        bool secondaryAction,
        bool allowDelay = true,
        bool forceSuppression = false
    )
    {
        var result = item.Activate(
            player,
            allowDelay ? config.Input.DelayedActions : DelayedActions.None,
            secondaryAction
        );
        Logger.Log(
            LogCategory.Activation,
            $"Activated {item.Title} with result: {result}",
            LogLevel.Info
        );
        switch (result)
        {
            case ItemActivationResult.Ignored:
                return result;
            case ItemActivationResult.Delayed:
                Sound.Play(config.Sound.ItemDelaySound);
                delayedItem = new(item, SecondaryAction: secondaryAction);
                break;
            default:
                ItemActivated?.Invoke(this, new(item, result));
                if (forceSuppression)
                {
                    activeMenu?.Toggle.ForceOff();
                }
                var activationSound = item.GetActivationSound(
                    player,
                    secondaryAction,
                    config.Sound.ItemActivationSound
                );
                Sound.Play(activationSound ?? "");
                Reset();
                break;
        }
        return result;
    }

    private void AnimateMenuOpen(TimeSpan elapsed)
    {
        if (activeMenu is null || menuOpenTimeMs >= QUICK_SLOT_ANIMATION_DURATION_MS)
        {
            return;
        }
        menuOpenTimeMs += (float)elapsed.TotalMilliseconds;
        var menuProgress = MathHelper.Clamp(menuOpenTimeMs / MENU_ANIMATION_DURATION_MS, 0, 1);
        menuScale = menuProgress < 1 ? 1 - MathF.Pow(1 - menuProgress, 3) : 1;
        fadeOpacity = 0.5f * (menuProgress < 1 ? MathF.Sin(menuProgress * MathF.PI / 2f) : 1);
        var quickSlotProgress = MathHelper.Clamp(
            menuOpenTimeMs / QUICK_SLOT_ANIMATION_DURATION_MS,
            0,
            1
        );
        quickSlotOpacity = quickSlotProgress < 1 ? MathF.Sin(quickSlotProgress * MathF.PI / 2f) : 1;
        Logger.Log(
            LogCategory.Menus,
            $"Menu animation frame: scale = {menuScale}, opacity = {fadeOpacity}",
            LogLevel.Trace
        );
    }

    private bool CheckStickActivation(bool secondaryAction)
    {
        if (activeMenu is null)
        {
            return false;
        }
        var activationMethod = secondaryAction
            ? config.Input.SecondaryActivationMethod
            : config.Input.PrimaryActivationMethod;
        if (activationMethod != ItemActivationMethod.ThumbStickPress)
        {
            return false;
        }
        var stickButton = config.Input.ThumbStickPreference switch
        {
            ThumbStickPreference.AlwaysLeft => SButton.LeftStick,
            ThumbStickPreference.AlwaysRight => SButton.RightStick,
            _ => activeMenu.Toggle.IsRightSided() ? SButton.RightStick : SButton.LeftStick,
        };
        var result = SuppressIfPressed(stickButton);
        if (result)
        {
            Logger.Log(
                LogCategory.Input,
                $"Detected thumbstick activation on {stickButton}, "
                    + $"secondary action = {secondaryAction}."
            );
        }
        return result;
    }

    private float GetSelectionBlend()
    {
        if (delayedItem is null)
        {
            return 1.0f;
        }
        var elapsed = (float)(
            config.Input.ActivationDelayMs - elapsedActivationDelay.TotalMilliseconds
        );
        return Animation.GetDelayFlashPosition(elapsed);
    }

    private void Reset()
    {
        Logger.Log(LogCategory.Menus, "Resetting menu controller state");
        delayedItem = null;
        elapsedActivationDelay = TimeSpan.Zero;
        focusedIndex = -1;
        focusedItem = null;
        cursorAngle = null;
        if (!config.Input.ReopenOnHold)
        {
            activeMenu?.Toggle.ForceOff();
        }
        activeMenu = null;
        menuOpenTimeMs = 0;
        menuScale = 0;
        quickSlotOpacity = 0;
        fadeOpacity = 0;
    }

    private bool SuppressIfPressed(SButton button)
    {
        if (inputHelper.GetState(button) != SButtonState.Pressed)
        {
            return false;
        }
        Logger.Log(LogCategory.Input, $"Suppressing pressed button {button}.");
        inputHelper.Suppress(button);
        return true;
    }

    private bool TryActivateDelayedItem(TimeSpan elapsed)
    {
        if (delayedItem is not { } activation)
        {
            return false;
        }
        elapsedActivationDelay += elapsed;
        Logger.Log(
            LogCategory.Menus,
            "Delayed activation pending, "
                + $"{elapsedActivationDelay.TotalMilliseconds:F0} / "
                + $"{config.Input.ActivationDelayMs} ms elapsed.",
            LogLevel.Trace
        );
        if (elapsedActivationDelay.TotalMilliseconds >= config.Input.ActivationDelayMs)
        {
            Logger.Log(
                LogCategory.Menus,
                $"Delay of {config.Input.ActivationDelayMs} ms expired; activating "
                    + $"{activation.Item.Title}.",
                LogLevel.Info
            );
            var result = activation.Item.Activate(
                player,
                DelayedActions.None,
                activation.SecondaryAction
            );
            Logger.Log(
                LogCategory.Activation,
                $"Activated {activation.Item.Title} with result: {result}",
                LogLevel.Info
            );
            ItemActivated?.Invoke(this, new(activation.Item, result));
            Reset();
        }
        // We still return true here, even if the delay hasn't expired, because a delayed activation
        // should prevent any other menu state from changing.
        return true;
    }

    private void TryActivateQuickSlot()
    {
        if (delayedItem is not null || cursorAngle is not null)
        {
            return;
        }
        var nextActivation = quickSlotController.TryGetNextActivation(
            activeMenu is not null,
            out var pressedButton
        );
        if (nextActivation is not null)
        {
            Logger.Log(
                LogCategory.QuickSlots,
                $"Quick slot activation detected for {nextActivation.Item.Title} in "
                    + $"{pressedButton} slot."
            );
            inputHelper.Suppress(pressedButton);
            Logger.Log(
                LogCategory.Input,
                $"Suppressed quick-slot activation button {pressedButton}."
            );
            if (nextActivation.RequireConfirmation)
            {
                Logger.Log(
                    LogCategory.QuickSlots,
                    "Confirmation required for quick slot; creating dialog."
                );
                var message = nextActivation.IsRegularItem
                    ? I18n.QuickSlotConfirmation_Item(nextActivation.Item.Title)
                    : I18n.QuickSlotConfirmation_Mod(nextActivation.Item.Title);
                Game1.activeClickableMenu = new ConfirmationDialog(
                    message,
                    _ =>
                    {
                        Logger.Log(
                            LogCategory.Activation,
                            "Activation confirmed from confirmation dialog."
                        );
                        Game1.activeClickableMenu = null;
                        ActivateItem(
                            nextActivation.Item,
                            nextActivation.SecondaryAction,
                            allowDelay: false,
                            // Forcing suppression here isn't done for any technical reason, it just seems more
                            // principle-of-least-surprise compliant not to have the menu immediately reopen or
                            // appear to stay open after e.g. switching a tool.
                            forceSuppression: true
                        );
                    },
                    onCancel: _ =>
                    {
                        Logger.Log(
                            LogCategory.Activation,
                            "Activation cancelled from confirmation dialog."
                        );
                    }
                );
            }
            else
            {
                var result = ActivateItem(
                    nextActivation.Item,
                    nextActivation.SecondaryAction,
                    allowDelay: activeMenu is not null
                );
                if (result == ItemActivationResult.Delayed)
                {
                    quickSlotController.ShowDelayedActivation(pressedButton);
                }
            }
        }
    }

    private void TryInteractWithActiveMenu()
    {
        if (activeMenu is null)
        {
            return;
        }

        activeMenu.Toggle.Update(allowOn: false);
        if (activeMenu.Toggle.State != MenuToggleState.On)
        {
            if (
                config.Input.PrimaryActivationMethod == ItemActivationMethod.TriggerRelease
                && focusedItem is not null
            )
            {
                Logger.Log(
                    LogCategory.Input,
                    "Trigger release activation detected for primary action."
                );
                ActivateItem(focusedItem, secondaryAction: false);
            }
            else if (
                config.Input.SecondaryActivationMethod == ItemActivationMethod.TriggerRelease
                && focusedItem is not null
            )
            {
                Logger.Log(
                    LogCategory.Input,
                    "Trigger release activation detected for secondary action."
                );
                ActivateItem(focusedItem, secondaryAction: true);
            }
            else
            {
                Sound.Play(config.Sound.MenuCloseSound);
                Reset();
            }
            return;
        }

        int previousPageIndex = activeMenu.SelectedPageIndex;
        if (SuppressIfPressed(config.Input.PreviousPageButton))
        {
            if (activeMenu.PreviousPage())
            {
                Sound.Play(config.Sound.PreviousPageSound);
                Logger.Log(
                    LogCategory.Menus,
                    "Navigated to previous page "
                        + $"({previousPageIndex} -> {activeMenu.SelectedPageIndex})."
                );
            }
            else
            {
                Logger.Log(LogCategory.Menus, "Couldn't navigate to previous page.");
            }
        }
        else if (SuppressIfPressed(config.Input.NextPageButton))
        {
            if (activeMenu.NextPage())
            {
                Sound.Play(config.Sound.NextPageSound);
                Logger.Log(
                    LogCategory.Menus,
                    "Navigated to next page "
                        + $"({previousPageIndex} -> {activeMenu.SelectedPageIndex})."
                );
            }
            else
            {
                Logger.Log(LogCategory.Menus, "Couldn't navigate to next page.");
            }
        }

        UpdateFocus(activeMenu);
        ActivateFocusedItem();
    }

    private void UpdateFocus(IRadialMenu menu)
    {
        var thumbsticks = Game1.input.GetGamePadState().ThumbSticks;
        var position = config.Input.ThumbStickPreference switch
        {
            ThumbStickPreference.AlwaysLeft => thumbsticks.Left,
            ThumbStickPreference.AlwaysRight => thumbsticks.Right,
            _ => menu.Toggle.IsRightSided() ? thumbsticks.Right : thumbsticks.Left,
        };
        float? angle =
            position.Length() > config.Input.ThumbstickDeadZone
                ? MathF.Atan2(position.X, position.Y)
                : null;
        cursorAngle = (angle + MathHelper.TwoPi) % MathHelper.TwoPi;
        if (cursorAngle is not null && menu.GetSelectedPage() is { Items.Count: > 0 } page)
        {
            var itemAngle = MathHelper.TwoPi / page.Items.Count;
            var nextFocusedIndex =
                (int)MathF.Round(cursorAngle.Value / itemAngle) % page.Items.Count;
            if (nextFocusedIndex != focusedIndex || page.Items[nextFocusedIndex] != focusedItem)
            {
                Logger.Log(
                    LogCategory.Menus,
                    $"Changed focused index from {focusedIndex} -> {nextFocusedIndex}. "
                        + $"(cursor angle = {cursorAngle} for {page.Items.Count} total items)"
                );
                Sound.Play(config.Sound.ItemFocusSound);
                focusedIndex = nextFocusedIndex;
                focusedItem = page.Items[focusedIndex];
            }
        }
        else
        {
            focusedIndex = -1;
            focusedItem = null;
        }
    }
}
