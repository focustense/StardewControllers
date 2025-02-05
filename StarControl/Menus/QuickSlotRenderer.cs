using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StarControl.Config;
using StarControl.Graphics;
using StardewValley.TerrainFeatures;

namespace StarControl.Menus;

internal class QuickSlotRenderer(GraphicsDevice graphicsDevice, ModConfig config)
{
    private record ButtonFlash(FlashType Type, float DurationMs, float ElapsedMs = 0);

    private enum FlashType
    {
        Delay,
        Error,
    }

    private enum PromptPosition
    {
        Above,
        Below,
        Left,
        Right,
    }

    public float BackgroundOpacity { get; set; } = 1;
    public float SpriteOpacity { get; set; } = 1;

    public IReadOnlyDictionary<SButton, IRadialMenuItem> SlotItems { get; set; } =
        new Dictionary<SButton, IRadialMenuItem>();
    public IReadOnlyDictionary<SButton, IItemLookup> Slots { get; set; } =
        new Dictionary<SButton, IItemLookup>();

    public bool UnassignedButtonsVisible { get; set; } = true;

    private const int BACKGROUND_RADIUS = SLOT_SIZE + SLOT_SIZE / 2 + MARGIN_OUTER;
    private const int IMAGE_SIZE = 64;
    private const int MARGIN_HORIZONTAL = 120;
    private const int MARGIN_OUTER = 32;
    private const int MARGIN_VERTICAL = 64;
    private const int PROMPT_OFFSET = SLOT_SIZE / 2;
    private const int PROMPT_SIZE = 32;
    private const int SLOT_PADDING = 20;
    private const int SLOT_SIZE = IMAGE_SIZE + SLOT_PADDING * 2;

    private static readonly Color OuterBackgroundColor = new(16, 16, 16, 210);

    private readonly Dictionary<SButton, ButtonFlash> flashes = [];
    private readonly HashSet<SButton> enabledSlots = [];
    private readonly Texture2D outerBackground = ShapeTexture.CreateCircle(
        SLOT_SIZE + SLOT_SIZE / 2 + MARGIN_OUTER,
        filled: true,
        graphicsDevice: graphicsDevice
    );
    private readonly Texture2D slotBackground = ShapeTexture.CreateCircle(
        SLOT_SIZE / 2,
        filled: true,
        graphicsDevice: graphicsDevice
    );
    private readonly Dictionary<SButton, Sprite> slotSprites = [];
    private readonly Texture2D uiTexture = Game1.content.Load<Texture2D>(Sprites.UI_TEXTURE_PATH);

    private Color disabledBackgroundColor = Color.Transparent;
    private Color innerBackgroundColor = Color.Transparent;
    private bool isDirty = true;

    public void Draw(SpriteBatch b, Rectangle viewport)
    {
        if (isDirty)
        {
            innerBackgroundColor = (Color)config.Style.OuterBackgroundColor * 0.6f;
            disabledBackgroundColor = LumaGray(innerBackgroundColor, 0.75f);
            RefreshSlots();
        }

        var leftOrigin = new Point(
            viewport.Left + MARGIN_HORIZONTAL + MARGIN_OUTER + SLOT_SIZE / 2,
            viewport.Bottom - MARGIN_VERTICAL - MARGIN_OUTER - SLOT_SIZE - SLOT_SIZE / 2
        );
        if (
            UnassignedButtonsVisible
            || enabledSlots.Contains(SButton.DPadLeft)
            || enabledSlots.Contains(SButton.DPadUp)
            || enabledSlots.Contains(SButton.DPadRight)
            || enabledSlots.Contains(SButton.DPadDown)
        )
        {
            var leftBackgroundRect = GetCircleRect(leftOrigin.AddX(SLOT_SIZE), BACKGROUND_RADIUS);
            b.Draw(outerBackground, leftBackgroundRect, OuterBackgroundColor * BackgroundOpacity);
            DrawSlot(b, leftOrigin, SButton.DPadLeft, PromptPosition.Left);
            DrawSlot(
                b,
                leftOrigin.Add(SLOT_SIZE, -SLOT_SIZE),
                SButton.DPadUp,
                PromptPosition.Above
            );
            DrawSlot(
                b,
                leftOrigin.Add(SLOT_SIZE, SLOT_SIZE),
                SButton.DPadDown,
                PromptPosition.Below
            );
            DrawSlot(b, leftOrigin.AddX(SLOT_SIZE * 2), SButton.DPadRight, PromptPosition.Right);
        }

        if (
            UnassignedButtonsVisible
            || enabledSlots.Contains(SButton.ControllerX)
            || enabledSlots.Contains(SButton.ControllerY)
            || enabledSlots.Contains(SButton.ControllerA)
            || enabledSlots.Contains(SButton.ControllerB)
        )
        {
            var rightOrigin = new Point(
                viewport.Right - MARGIN_HORIZONTAL - MARGIN_OUTER - SLOT_SIZE / 2,
                leftOrigin.Y
            );
            var rightBackgroundRect = GetCircleRect(
                rightOrigin.AddX(-SLOT_SIZE),
                BACKGROUND_RADIUS
            );
            b.Draw(outerBackground, rightBackgroundRect, OuterBackgroundColor * BackgroundOpacity);
            DrawSlot(b, rightOrigin, SButton.ControllerB, PromptPosition.Right);
            DrawSlot(
                b,
                rightOrigin.Add(-SLOT_SIZE, -SLOT_SIZE),
                SButton.ControllerY,
                PromptPosition.Above
            );
            DrawSlot(
                b,
                rightOrigin.Add(-SLOT_SIZE, SLOT_SIZE),
                SButton.ControllerA,
                PromptPosition.Below
            );
            DrawSlot(b, rightOrigin.AddX(-SLOT_SIZE * 2), SButton.ControllerX, PromptPosition.Left);
        }
    }

    public void FlashDelay(SButton button)
    {
        flashes[button] = new(FlashType.Delay, config.Input.ActivationDelayMs);
    }

    public void FlashError(SButton button)
    {
        flashes[button] = new(FlashType.Error, Animation.ERROR_FLASH_DURATION_MS);
    }

    public void Invalidate()
    {
        isDirty = true;
    }

    public void Update(TimeSpan elapsed)
    {
        foreach (var (button, flash) in flashes)
        {
            var flashElapsedMs = flash.ElapsedMs + (float)elapsed.TotalMilliseconds;
            if (flashElapsedMs >= flash.DurationMs)
            {
                flashes.Remove(button);
                continue;
            }
            flashes[button] = flash with { ElapsedMs = flashElapsedMs };
        }
    }

    private void DrawSlot(
        SpriteBatch b,
        Point origin,
        SButton button,
        PromptPosition promptPosition
    )
    {
        var isAssigned = slotSprites.TryGetValue(button, out var sprite);
        if (!isAssigned && !UnassignedButtonsVisible)
        {
            return;
        }

        var isEnabled = enabledSlots.Contains(button);
        var backgroundRect = GetCircleRect(origin, SLOT_SIZE / 2);
        var backgroundColor = GetBackgroundColor(button, isAssigned && isEnabled);
        b.Draw(slotBackground, backgroundRect, backgroundColor * BackgroundOpacity);

        var slotOpacity = isEnabled ? 1f : 0.5f;

        if (isAssigned)
        {
            var spriteRect = GetCircleRect(origin, IMAGE_SIZE / 2);
            b.Draw(
                sprite!.Texture,
                spriteRect,
                sprite.SourceRect,
                Color.White * slotOpacity * SpriteOpacity
            );
        }

        if (GetPromptSprite(button) is { } promptSprite)
        {
            var promptOrigin = promptPosition switch
            {
                PromptPosition.Above => origin.AddY(-PROMPT_OFFSET),
                PromptPosition.Below => origin.AddY(PROMPT_OFFSET),
                PromptPosition.Left => origin.AddX(-PROMPT_OFFSET),
                PromptPosition.Right => origin.AddX(PROMPT_OFFSET),
                _ => throw new ArgumentException(
                    $"Invalid prompt position: {promptPosition}",
                    nameof(promptPosition)
                ),
            };
            var promptRect = GetCircleRect(promptOrigin, PROMPT_SIZE / 2);
            b.Draw(
                promptSprite.Texture,
                promptRect,
                promptSprite.SourceRect,
                Color.White * slotOpacity * SpriteOpacity
            );
        }
    }

    private Color GetBackgroundColor(SButton button, bool enabled)
    {
        var baseColor = enabled ? innerBackgroundColor : disabledBackgroundColor;
        if (!flashes.TryGetValue(button, out var flash))
        {
            return baseColor;
        }
        var (flashColor, position) = flash.Type switch
        {
            FlashType.Delay => (
                config.Style.HighlightColor,
                Animation.GetDelayFlashPosition(flash.ElapsedMs)
            ),
            FlashType.Error => (Color.Red, Animation.GetErrorFlashPosition(flash.ElapsedMs)),
            _ => (Color.White, 0),
        };
        return Color.Lerp(baseColor, flashColor, position);
    }

    private static Rectangle GetCircleRect(Point center, int radius)
    {
        int length = radius * 2;
        return new(center.X - radius, center.Y - radius, length, length);
    }

    private static Sprite GetIconSprite(IconConfig icon)
    {
        return !string.IsNullOrEmpty(icon.ItemId)
            ? Sprite.ForItemId(icon.ItemId)
            : Sprite.TryLoad(icon.TextureAssetPath, icon.SourceRect)
                ?? Sprite.ForItemId("Error_Invalid");
    }

    private Sprite? GetModItemSprite(string id)
    {
        var itemConfig = config
            .Items.ModMenuPages.SelectMany(items => items)
            .FirstOrDefault(item => item.Id == id);
        return itemConfig?.Icon is { } icon ? GetIconSprite(icon) : null;
    }

    private Sprite? GetPromptSprite(SButton button)
    {
        var (rowIndex, columnIndex) = button switch
        {
            SButton.DPadUp => (1, 0),
            SButton.DPadRight => (1, 1),
            SButton.DPadDown => (1, 2),
            SButton.DPadLeft => (1, 3),
            SButton.ControllerA => (1, 4),
            SButton.ControllerB => (1, 5),
            SButton.ControllerX => (1, 6),
            SButton.ControllerY => (1, 7),
            SButton.LeftTrigger => (2, 0),
            SButton.RightTrigger => (2, 1),
            SButton.LeftShoulder => (2, 2),
            SButton.RightShoulder => (2, 3),
            SButton.ControllerBack => (2, 4),
            SButton.ControllerStart => (2, 5),
            SButton.LeftStick => (2, 6),
            SButton.RightStick => (2, 7),
            _ => (-1, -1),
        };
        if (columnIndex == -1)
        {
            return null;
        }
        return new(uiTexture, new(columnIndex * 16, rowIndex * 16, 16, 16));
    }

    private Sprite? GetSlotSprite(IItemLookup itemLookup)
    {
        if (string.IsNullOrWhiteSpace(itemLookup.Id))
        {
            return null;
        }
        return itemLookup.IdType switch
        {
            ItemIdType.GameItem => Sprite.ForItemId(itemLookup.Id),
            ItemIdType.ModItem => GetModItemSprite(itemLookup.Id),
            _ => null,
        };
    }

    private static Color LumaGray(Color color, float lightness)
    {
        var v = (int)((color.R * 0.2126f + color.G * 0.7152f + color.B * 0.0722f) * lightness);
        return new(v, v, v);
    }

    private void RefreshSlots()
    {
        Logger.Log(LogCategory.QuickSlots, "Starting refresh of quick slot renderer data.");
        enabledSlots.Clear();
        slotSprites.Clear();
        foreach (var (button, slotConfig) in Slots)
        {
            Logger.Log(LogCategory.QuickSlots, $"Checking slot for {button}...");
            Sprite? sprite = null;
            if (SlotItems.TryGetValue(button, out var item))
            {
                if (item.Texture is not null)
                {
                    Logger.Log(
                        LogCategory.QuickSlots,
                        $"Using configured item sprite for {item.Title} in {button} slot."
                    );
                    sprite = new(item.Texture, item.SourceRectangle ?? item.Texture.Bounds);
                }
                else
                {
                    Logger.Log(
                        LogCategory.QuickSlots,
                        $"Item {item.Title} in {button} slot has no texture; using default sprite."
                    );
                }
                enabledSlots.Add(button);
                Logger.Log(
                    LogCategory.QuickSlots,
                    $"Enabled {button} slot with '{item.Title}'.",
                    LogLevel.Info
                );
            }
            else
            {
                Logger.Log(
                    LogCategory.QuickSlots,
                    $"Disabled unassigned {button} slot.",
                    LogLevel.Info
                );
            }
            sprite ??= GetSlotSprite(slotConfig);
            if (sprite is not null)
            {
                slotSprites.Add(button, sprite);
            }
        }
        isDirty = false;
    }
}

file static class PointExtensions
{
    public static Point Add(this Point point, int x, int y)
    {
        return new(point.X + x, point.Y + y);
    }

    public static Point AddX(this Point point, int x)
    {
        return new(point.X + x, point.Y);
    }

    public static Point AddY(this Point point, int y)
    {
        return new(point.X, point.Y + y);
    }
}
