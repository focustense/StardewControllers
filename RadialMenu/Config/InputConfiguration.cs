﻿namespace RadialMenu.Config;

/// <summary>
/// Configures the controller menu's interactions, including key bindings and activation behavior.
/// </summary>
public class InputConfiguration
{
    /// <summary>
    /// Button to open the inventory wheel during normal gameplay.
    /// </summary>
    public SButton InventoryMenuButton { get; set; } = SButton.LeftTrigger;

    /// <summary>
    /// Button to open the mod action wheel during normal gameplay.
    /// </summary>
    public SButton ModMenuButton { get; set; } = SButton.RightTrigger;

    /// <summary>
    /// Button to cycle to the previous page of any open wheel.
    /// </summary>
    public SButton PreviousPageButton { get; set; } = SButton.LeftShoulder;

    /// <summary>
    /// Button to cycle to the next page of any open wheel.
    /// </summary>
    public SButton NextPageButton { get; set; } = SButton.RightShoulder;

    /// <summary>
    /// Button to perform the primary action on the selected menu item (typically "use").
    /// </summary>
    public SButton PrimaryActionButton { get; set; } = SButton.ControllerA;

    /// <summary>
    /// Button to perform the secondary action on the selected menu item (typically "select").
    /// </summary>
    public SButton SecondaryActionButton { get; set; } = SButton.ControllerX;

    /// <summary>
    /// Selects which thumbstick is used to navigate the wheel after opening.
    /// </summary>
    public ThumbStickPreference ThumbStickPreference { get; set; } =
        ThumbStickPreference.AlwaysLeft;

    /// <summary>
    /// Whether the <see cref="InventoryMenuButton"/> or <see cref="ModMenu"/> are used to hold the
    /// menu open (default) or toggle it on/off.
    /// </summary>
    public MenuOpenMode OpenMode { get; set; } = MenuOpenMode.Hold;

    /// <summary>
    /// Specifies which actions chosen from the inventory/mod wheels should be delayed (blink)
    /// before being executed.
    /// </summary>
    public DelayedActions DelayedActions { get; set; } = DelayedActions.ToolSwitch;

    /// <summary>
    /// Duration to hold the menu open (ignoring further inputs, and with gameplay paused) and blink
    /// the selected item before the item activation completes. Only applies to actions meeting the
    /// <see cref="DelayedActions"/> criteria.
    /// </summary>
    public int ActivationDelayMs { get; set; } = 250;

    /// <summary>
    /// Whether or not to remember the player's previous selection per menu.
    /// </summary>
    /// <remarks>
    /// If enabled, closing and reopening a menu will cause it to reopen to the last open page,
    /// instead of resetting to the default (current backpack page for inventory, first page for
    /// custom menu).
    /// </remarks>
    public bool RememberSelection;

    /// <summary>
    /// Dead zone for the left/right trigger buttons for activating/deactivating the menu.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Triggers are generally used as regular buttons in Stardew Valley, but are technically
    /// analog inputs. Due to a variety of technical issues, this mod needs to ignore the
    /// simpler on/off behavior and read the analog input directly. Increase the dead zone if
    /// necessary to prevent accidental presses, or reduce it for hair-trigger response.
    /// </para>
    /// <para>
    /// Only applicable when either or both of the <see cref="InventoryMenuButton"/> and
    /// <see cref="ModMenuButton"/> properties are set to use triggers.
    /// </para>
    /// </remarks>
    public float TriggerDeadZone { get; set; } = 0.2f;

    /// <summary>
    /// Dead-zone for the thumbstick when selecting from a radial menu.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only applies to menu selection; changing this setting will not affect the dead zone used
    /// for any other mods or controls in the vanilla game.
    /// </para>
    /// <para>
    /// Many, if not most controllers suffer from drift issues in the analog sticks. Setting
    /// this value too low could cause items to get selected even when the thumbstick has not
    /// been moved.
    /// </para>
    /// </remarks>
    public float ThumbstickDeadZone { get; set; } = 0.2f;
}
