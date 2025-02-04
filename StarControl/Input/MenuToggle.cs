using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StarControl.Config;

namespace StarControl.Input;

/// <summary>
/// Menu toggle implementation based on a hybrid of SMAPI and direct XNA/MonoGame APIs.
/// </summary>
/// <remarks>
/// Ensures correct <see cref="State"/> transitions and works around certain limitations of SMAPI
/// input suppressions during standard gameplay (farmer-world interaction).
/// </remarks>
/// <param name="inputHelper">SMAPI input helper, used to check button states.</param>
/// <param name="inputConfig">The current input settings.</param>
public class MenuToggle(
    IInputHelper inputHelper,
    InputConfiguration inputConfig,
    Func<InputConfiguration, SButton> buttonSelector
) : IMenuToggle
{
    private const int FORCED_SUPPRESSION_DURATION_TICKS = 3;

    /// <inheritdoc />
    public MenuToggleState State { get; private set; }

    private SButton Button => buttonSelector(inputConfig);
    private MenuToggleMode Mode => inputConfig.ToggleMode;
    private float TriggerDeadZone => inputConfig.TriggerDeadZone;

    private int forcedButtonSuppressionTicks;
    private GamePadState gamePadState;
    private bool hasForcedButtonSuppression;
    private bool wasDown;
    private bool wasReleasedWhileNonInteractive;

    /// <inheritdoc />
    public void ForceButtonSuppression()
    {
        if (Mode != MenuToggleMode.Hold || !RequiresSmapiBypass(Button))
        {
            return;
        }
        hasForcedButtonSuppression = true;
        forcedButtonSuppressionTicks = 0;
    }

    /// <inheritdoc />
    public void ForceOff()
    {
        ForceButtonSuppression();
        if (State == MenuToggleState.Off)
        {
            return;
        }
        State = Mode == MenuToggleMode.Hold ? MenuToggleState.Suppressed : MenuToggleState.Off;
        Logger.Log(LogCategory.Input, $"Toggle for {Button} was forced off; new state is {State}.");
    }

    /// <inheritdoc />
    public bool IsRightSided()
    {
        return Button
            is SButton.ControllerA
                or SButton.ControllerB
                or SButton.ControllerX
                or SButton.ControllerY
                or SButton.RightStick
                or SButton.RightTrigger
                or SButton.RightShoulder;
    }

    /// <inheritdoc />
    public void PreUpdate(bool interactive)
    {
        // Forced button suppression is an unwieldy but seemingly effective hack for dealing with
        // trigger presses "leaking" into the menu due to disagreement between vanilla and SMAPI
        // button states, along with the fact that we proactively suppress the trigger during
        // interactive frames. These factors together cause the game to think that the trigger was
        // immediately pressed and then released, when it has only been released after a suppressed
        // down state.
        //
        // There isn't a very clean way around it, so what we do is simply continue suppressing the
        // button until a few ticks AFTER we detect (bypassing SMAPI's detection) that it has
        // actually been released. At the end of these ticks, the button has already transitioned
        // through the dead zone and SDV/SMAPI perceive it as simply up - now and previously.
        if ((wasDown && !wasReleasedWhileNonInteractive) || hasForcedButtonSuppression)
        {
            gamePadState = GetRawGamePadState();
        }
        if (wasDown && !wasReleasedWhileNonInteractive && !IsButtonDown(Button))
        {
            Logger.Log(LogCategory.Input, $"Button was released during non-interactive update.");
            wasReleasedWhileNonInteractive = true;
        }
        if (hasForcedButtonSuppression)
        {
            if (forcedButtonSuppressionTicks >= FORCED_SUPPRESSION_DURATION_TICKS)
            {
                hasForcedButtonSuppression = false;
            }
            else if (forcedButtonSuppressionTicks > 0 || !IsButtonDown(Button))
            {
                forcedButtonSuppressionTicks++;
            }
            inputHelper.Suppress(Button);
        }

        if (interactive && RequiresSmapiBypass(Button))
        {
            Logger.LogOnce(
                LogCategory.Input,
                $"Button {Button} used for menu toggle requires SMAPI bypass due to timing/state "
                    + "discrepancies and will be suppressed before every update tick."
            );
            inputHelper.Suppress(Button);
        }
    }

    /// <inheritdoc />
    public void Update(bool allowOn)
    {
        gamePadState = GetRawGamePadState();
        var isDown = IsButtonDown(Button);
        if (isDown != wasDown)
        {
            Logger.Log(
                LogCategory.Input,
                $"Down state for {Button} changed from {wasDown} -> {isDown}."
            );
        }
        var nextState = State switch
        {
            MenuToggleState.Off when !wasDown && isDown => allowOn
                ? MenuToggleState.On
                : MenuToggleState.Wait,
            MenuToggleState.Wait when isDown && allowOn => MenuToggleState.On,
            MenuToggleState.Wait when !isDown && Mode == MenuToggleMode.Hold => MenuToggleState.Off,
            MenuToggleState.On when !isDown && Mode == MenuToggleMode.Hold => MenuToggleState.Off,
            MenuToggleState.On when !wasDown && isDown && Mode == MenuToggleMode.Toggle =>
                MenuToggleState.Off,
            MenuToggleState.Suppressed when isDown && wasReleasedWhileNonInteractive =>
                MenuToggleState.On,
            MenuToggleState.Suppressed when !isDown => MenuToggleState.Off,
            _ => State,
        };
        if (nextState != State)
        {
            Logger.LogOnce(
                LogCategory.Input,
                $"Toggle state is changing from {State} -> {nextState}. (Current mode: {Mode})"
            );
        }
        State = nextState;
        wasDown = isDown;
        if (isDown && !RequiresSmapiBypass(Button))
        {
            Logger.Log(
                LogCategory.Input,
                $"Suppressing button {Button} which triggered state change."
            );
            inputHelper.Suppress(Button);
        }
        wasReleasedWhileNonInteractive = false;
    }

    private static GamePadState GetRawGamePadState()
    {
        return Game1.playerOneIndex >= PlayerIndex.One
            ? GamePad.GetState(Game1.playerOneIndex)
            : new();
    }

    private bool IsButtonDown(SButton button)
    {
        return button switch
        {
            SButton.LeftTrigger => gamePadState.Triggers.Left > TriggerDeadZone,
            SButton.RightTrigger => gamePadState.Triggers.Right > TriggerDeadZone,
            _ => inputHelper.IsDown(button)
                || (
                    State == MenuToggleState.On
                    && Mode == MenuToggleMode.Hold
                    && inputHelper.IsSuppressed(button)
                ),
        };
    }

    // The usual approach to handling a keybind is watch for it, and then after it is detected,
    // suppress it with SMAPI's input helper to block its vanilla function. This doesn't work with
    // some buttons due to differences between SMAPI's dead zone and/or timing, and Stardew's. When
    // one of these buttons is used for menu activation, we instead have to constantly suppress the
    // vanilla input, and bypass SMAPI's input helper to get the raw value of the axis.
    private static bool RequiresSmapiBypass(SButton button)
    {
        return button is SButton.LeftTrigger or SButton.RightTrigger;
    }
}
