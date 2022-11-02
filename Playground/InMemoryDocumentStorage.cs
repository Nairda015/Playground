namespace Playground;

internal class InMemoryDocumentStorage
{
    private readonly Dictionary<long, Product> _products = new();
    
    public bool TryGetProduct(long id, out Product product)
    {
        return _products.TryGetValue(id, out product);
    }
    
    public void SaveProduct(Product product)
    {
        _products[product.Id] = product;
    }
    
    public List<Product> GetAllProducts()
    {
        return _products.Values.ToList();
    }
}
