﻿using PropertyChanged.SourceGenerator;
using StarControl.Config;

namespace StarControl.UI;

internal partial class InputConfigurationViewModel
{
    public EnumSegmentsViewModel<DelayedActions> DelayedActions { get; } = new();
    public Func<float, string> FormatActivationDelay { get; } = v => $"{v:f0} ms";
    public Func<float, string> FormatDeadZone { get; } = v => v.ToString("f2");
    public EnumSegmentsViewModel<ItemActivationMethod> PrimaryActivationMethod { get; } = new();
    public EnumSegmentsViewModel<ItemActivationMethod> SecondaryActivationMethod { get; } = new();
    public EnumSegmentsViewModel<ThumbStickPreference> ThumbStickPreference { get; } = new();
    public EnumSegmentsViewModel<MenuToggleMode> ToggleMode { get; } = new();

    [Notify]
    private int activationDelayMs;

    [Notify]
    private SButton inventoryMenuButton;

    [Notify]
    private SButton modMenuButton;

    [Notify]
    private SButton previousPageButton;

    [Notify]
    private SButton nextPageButton;

    [Notify]
    private SButton primaryActionButton;

    [Notify]
    private SButton secondaryActionButton;

    [Notify]
    private SButton remappingMenuButton;

    [Notify]
    private SButton remappingHudButton;

    [Notify]
    private bool reopenOnHold;

    [Notify]
    private bool reopenOnHoldDisabled;

    [Notify]
    private bool rememberSelection;

    [Notify]
    private bool suppressRightStickChatBox;

    [Notify]
    private float triggerDeadZone;

    [Notify]
    private float thumbstickDeadZone;

    public InputConfigurationViewModel()
    {
        ToggleMode.ValueChanged += ToggleMode_ValueChanged;
    }

    private void ToggleMode_ValueChanged(object? sender, EventArgs e)
    {
        ReopenOnHoldDisabled = ToggleMode.SelectedValue != MenuToggleMode.Hold;
        if (ReopenOnHoldDisabled)
        {
            ReopenOnHold = false;
        }
    }

    public void Load(InputConfiguration config)
    {
        InventoryMenuButton = config.InventoryMenuButton;
        ModMenuButton = config.ModMenuButton;
        PreviousPageButton = config.PreviousPageButton;
        NextPageButton = config.NextPageButton;
        PrimaryActionButton = config.PrimaryActionButton;
        PrimaryActivationMethod.SelectedValue = config.PrimaryActivationMethod;
        SecondaryActionButton = config.SecondaryActionButton;
        SecondaryActivationMethod.SelectedValue = config.SecondaryActivationMethod;
        ThumbStickPreference.SelectedValue = config.ThumbStickPreference;
        ToggleMode.SelectedValue = config.ToggleMode;
        RemappingMenuButton = config.RemappingMenuButton;
        remappingHudButton = config.RemappingHudButton;
        ReopenOnHold = config.ReopenOnHold;
        DelayedActions.SelectedValue = config.DelayedActions;
        ActivationDelayMs = config.ActivationDelayMs;
        RememberSelection = config.RememberSelection;
        SuppressRightStickChatBox = config.SuppressRightStickChatBox;
        TriggerDeadZone = config.TriggerDeadZone;
        ThumbstickDeadZone = config.ThumbstickDeadZone;
    }

    public void OnAdvancedButtonHover()
    {
        Game1.playSound("Cowboy_Footstep");
    }

    public void OpenAdvanced()
    {
        ViewEngine.OpenChildMenu("ControlsAdvanced", this);
    }

    public void Save(InputConfiguration config)
    {
        config.InventoryMenuButton = InventoryMenuButton;
        config.ModMenuButton = ModMenuButton;
        config.PreviousPageButton = PreviousPageButton;
        config.NextPageButton = NextPageButton;
        config.PrimaryActionButton = PrimaryActionButton;
        config.PrimaryActivationMethod = PrimaryActivationMethod.SelectedValue;
        config.SecondaryActionButton = SecondaryActionButton;
        config.SecondaryActivationMethod = SecondaryActivationMethod.SelectedValue;
        config.ThumbStickPreference = ThumbStickPreference.SelectedValue;
        config.ToggleMode = ToggleMode.SelectedValue;
        config.RemappingMenuButton = RemappingMenuButton;
        config.RemappingHudButton = remappingHudButton;
        config.ReopenOnHold = ReopenOnHold;
        config.DelayedActions = DelayedActions.SelectedValue;
        config.ActivationDelayMs = ActivationDelayMs;
        config.RememberSelection = RememberSelection;
        config.SuppressRightStickChatBox = SuppressRightStickChatBox;
        config.TriggerDeadZone = TriggerDeadZone;
        config.ThumbstickDeadZone = ThumbstickDeadZone;
    }
}
