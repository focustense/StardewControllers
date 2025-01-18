namespace RadialMenu.Config;

/// <summary>
/// Configuration for a Quick Slot that binds actions to a single button press while one of the
/// radial menus is open.
/// </summary>
public class QuickSlotConfiguration
{
    /// <summary>
    /// The type of ID that the <see cref="Id"/> refers to.
    /// </summary>
    public ItemIdType IdType { get; set; }

    /// <summary>
    /// Identifies which item to select or use. Can be the ID of a regular game item or of a Mod
    /// Menu action, depending on the <see cref="IdType"/>.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Whether to display a confirmation dialog before activating the item in this slot.
    /// </summary>
    public bool RequireConfirmation { get; set; }

    /// <summary>
    /// Whether to perform the item's secondary action (generally "Select") instead of its primary
    /// action (generally "Consume/Use"), if a secondary action exists. If the item only has one
    /// possible action, this setting is ignored.
    /// </summary>
    public bool UseSecondaryAction { get; set; }
}
