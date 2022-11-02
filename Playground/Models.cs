namespace Playground;

public record Price(DateOnly Date, decimal Value);
public record Product(long Id, string Name, string Category);
public record ProductPriceChanged(long ShopId, decimal NewPrice);

public record ProductAddedToShop(long ShopChainId, decimal InitialPrice);

public record ProductPriceView
{
    public long Id { get; init; }
    public string Name { get; init; }
    public string Category { get; init; }
    public List<Price> Prices { get; } = new();
}