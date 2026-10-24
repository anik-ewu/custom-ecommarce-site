namespace Shop.Domain.Catalog;

public enum InventoryType
{
    /// <summary>One of a kind: stock is 1 and the piece is reserved while in a cart.</summary>
    Unique,
    /// <summary>Sold by quantity; stock is checked and decremented at checkout.</summary>
    Standard,
}
