﻿namespace StarControl.Config;

/// <summary>
/// Methods of activating an item in a radial menu.
/// </summary>
public enum ItemActivationMethod
{
    /// <summary>
    /// Activate by pressing the assigned button/keybind.
    /// </summary>
    Keybind,

    /// <summary>
    /// Activate by pressing the same thumbstick that is being used for selection.
    /// </summary>
    /// <remarks>
    /// This is a middle-of-the-road option that provides the benefit of explicit activation,
    /// without needing to worry about accidental trigger release, but is more compatible with
    /// right-thumbstick preferences (<see cref="ThumbStickPreference.AlwaysRight"/> or
    /// <see cref="ThumbStickPreference.SameAsTrigger"/>). However, ease of use depends heavily
    /// on the controller, and on some controllers it may be difficult to press the thumbstick
    /// without moving it.
    /// </remarks>
    ThumbStickPress,

    /// <summary>
    /// Activate whichever item was last selected when the menu trigger is released.
    /// </summary>
    /// <remarks>
    /// This mode is better optimized for fast-paced (speedrun/minmax) gameplay as it allows the
    /// radials to be operated using only two inputs instead of three. However, players who are
    /// not very experienced with radial menus might find it error-prone due to accidental
    /// movement of the thumbstick while releasing the trigger.
    /// </remarks>
    TriggerRelease,
};
