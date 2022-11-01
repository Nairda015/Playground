namespace Playground;

internal class InMemoryDocumentStorage
{
    public Dictionary<long, Product> Products { get; set; } = new();
}
