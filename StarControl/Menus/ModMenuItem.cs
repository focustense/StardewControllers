﻿using Microsoft.Xna.Framework.Graphics;

namespace StarControl.Menus;

/// <summary>
/// An immutable menu item with user-defined properties.
/// </summary>
/// <param name="id">Unique ID for the item, specified by configuration.</param>
/// <param name="title">The <see cref="IRadialMenuItem.Title"/>.</param>
/// <param name="activate">A delegate for the <see cref="IRadialMenuItem.Activate"/> method.</param>
/// <param name="description">The <see cref="IRadialMenuItem.Description"/>.</param>
/// <param name="stackSize">The <see cref="IRadialMenuItem.StackSize"/>.</param>
/// <param name="quality">The <see cref="IRadialMenuItem.Quality"/>.</param>
/// <param name="texture">The <see cref="IRadialMenuItem.Texture"/>.</param>
/// <param name="sourceRectangle">The <see cref="IRadialMenuItem.SourceRectangle"/>.</param>
/// <param name="tintRectangle">The <see cref="IRadialMenuItem.TintRectangle"/>.</param>
/// <param name="tintColor">The <see cref="IRadialMenuItem.TintColor"/>.</param>
internal class ModMenuItem(
    string id,
    Func<string> title,
    Func<Farmer, DelayedActions, ItemActivationType, ItemActivationResult> activate,
    Func<string?>? description = null,
    int? stackSize = null,
    int? quality = null,
    Func<Texture2D?>? texture = null,
    Func<Rectangle?>? sourceRectangle = null,
    Func<Rectangle?>? tintRectangle = null,
    Color? tintColor = null
) : IRadialMenuItem
{
    public string Id { get; } = id;
    public string Title { get; } = title();

    public string Description { get; } = description?.Invoke() ?? "";

    public int? StackSize { get; } = stackSize;

    public int? Quality { get; } = quality;

    public Texture2D? Texture => texture?.Invoke();

    public Rectangle? SourceRectangle => sourceRectangle?.Invoke();

    public Rectangle? TintRectangle => tintRectangle?.Invoke();

    public Color? TintColor { get; } = tintColor;

    public ItemActivationResult Activate(
        Farmer who,
        DelayedActions delayedActions,
        ItemActivationType activationType
    )
    {
        return activate(who, delayedActions, activationType);
    }
}
