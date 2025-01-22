using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadialMenu.Config;
using RadialMenu.Graphics;

namespace RadialMenu.Menus;

public class QuickSlotRenderer(GraphicsDevice graphicsDevice, ModConfig config)
{
    private enum PromptPosition
    {
        Above,
        Below,
        Left,
        Right,
    }

    private const int BACKGROUND_RADIUS = SLOT_SIZE + SLOT_SIZE / 2 + MARGIN_OUTER;
    private const int IMAGE_SIZE = 64;
    private const int MARGIN_HORIZONTAL = 64;
    private const int MARGIN_OUTER = 32;
    private const int MARGIN_VERTICAL = 64;
    private const int PROMPT_OFFSET = SLOT_SIZE / 2;
    private const int PROMPT_SIZE = 32;
    private const int SLOT_PADDING = 20;
    private const int SLOT_SIZE = IMAGE_SIZE + SLOT_PADDING * 2;

    private static readonly Color OuterBackgroundColor = new(16, 16, 16, 210);

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

    private Color innerBackgroundColor = Color.Transparent;
    private bool isDirty = true;

    public void Draw(SpriteBatch b, Rectangle viewport)
    {
        if (isDirty)
        {
            innerBackgroundColor = (Color)config.Style.OuterBackgroundColor * 0.6f;
            RefreshSlots();
        }

        var leftOrigin = new Point(
            viewport.Left + MARGIN_HORIZONTAL + MARGIN_OUTER + SLOT_SIZE / 2,
            viewport.Bottom - MARGIN_VERTICAL - MARGIN_OUTER - SLOT_SIZE - SLOT_SIZE / 2
        );
        var leftBackgroundRect = GetCircleRect(leftOrigin.AddX(SLOT_SIZE), BACKGROUND_RADIUS);
        b.Draw(outerBackground, leftBackgroundRect, OuterBackgroundColor);
        DrawSlot(b, leftOrigin, SButton.DPadLeft, PromptPosition.Left);
        DrawSlot(b, leftOrigin.Add(SLOT_SIZE, -SLOT_SIZE), SButton.DPadUp, PromptPosition.Above);
        DrawSlot(b, leftOrigin.Add(SLOT_SIZE, SLOT_SIZE), SButton.DPadDown, PromptPosition.Below);
        DrawSlot(b, leftOrigin.AddX(SLOT_SIZE * 2), SButton.DPadRight, PromptPosition.Right);

        var rightOrigin = new Point(
            viewport.Right - MARGIN_HORIZONTAL - MARGIN_OUTER - SLOT_SIZE / 2,
            leftOrigin.Y
        );
        var rightBackgroundRect = GetCircleRect(rightOrigin.AddX(-SLOT_SIZE), BACKGROUND_RADIUS);
        b.Draw(outerBackground, rightBackgroundRect, OuterBackgroundColor);
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

    public void Invalidate()
    {
        isDirty = true;
    }

    private void DrawSlot(
        SpriteBatch b,
        Point origin,
        SButton button,
        PromptPosition promptPosition
    )
    {
        var backgroundRect = GetCircleRect(origin, SLOT_SIZE / 2);
        b.Draw(slotBackground, backgroundRect, innerBackgroundColor);

        var promptOpacity = 0.5f;
        if (slotSprites.TryGetValue(button, out var sprite))
        {
            var spriteRect = GetCircleRect(origin, IMAGE_SIZE / 2);
            b.Draw(sprite.Texture, spriteRect, sprite.SourceRect, Color.White);
            promptOpacity = 1;
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
                Color.White * promptOpacity
            );
        }
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
        return itemConfig is not null ? GetIconSprite(itemConfig.Icon) : null;
    }

    private Sprite? GetPromptSprite(SButton button)
    {
        var columnIndex = button switch
        {
            SButton.DPadUp => 0,
            SButton.DPadRight => 1,
            SButton.DPadDown => 2,
            SButton.DPadLeft => 3,
            SButton.ControllerA => 4,
            SButton.ControllerB => 5,
            SButton.ControllerX => 6,
            SButton.ControllerY => 7,
            _ => -1,
        };
        if (columnIndex == -1)
        {
            return null;
        }
        return new(uiTexture, new(columnIndex * 16, 16, 16, 16));
    }

    private Sprite? GetSlotSprite(QuickSlotConfiguration slotConfig)
    {
        if (string.IsNullOrWhiteSpace(slotConfig.Id))
        {
            return null;
        }
        return slotConfig.IdType switch
        {
            ItemIdType.GameItem => Sprite.ForItemId(slotConfig.Id),
            ItemIdType.ModItem => GetModItemSprite(slotConfig.Id),
            _ => null,
        };
    }

    private void RefreshSlots()
    {
        slotSprites.Clear();
        foreach (var (button, slotConfig) in config.Items.QuickSlots)
        {
            if (GetSlotSprite(slotConfig) is { } sprite)
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
