using System.Text.Json;
using EventStore.Client;
using Playground;
using static Playground.IdGen;

var builder = WebApplication.CreateBuilder(args);

#region Configure Services

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => { c.OrderActionsBy(x => x.HttpMethod); });

builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<InMemoryDocumentStorage>();
builder.Services.AddEventStoreClient("esdb://localhost:2113?tls=false", x => 
{
    x.DefaultDeadline = TimeSpan.FromSeconds(5);
});

#endregion

var app = builder.Build();

#region Configure Pipeline

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
app.UseSwagger();
app.UseHttpsRedirection();
app.UseRouting();

#endregion

app.MapGet("/api/health", () => "Playground is healthy");

#region InMemoryDocumentStorage requests

app.MapPost("product/{name}",  (InMemoryDocumentStorage storage, string name) =>
{
    //sub from products
    var prodId = NextId();
    var product = new Product(prodId, name, "TestCategory");
    storage.SaveProduct(product);
    return Results.Created("product/{productId:long}", product);
});

app.MapGet("product/{productId:long}", (InMemoryDocumentStorage storage, long productId) =>
        storage.TryGetProduct(productId, out var product)
        ? Results.Ok(product)
        : Results.NotFound());

app.MapGet("/products", (InMemoryDocumentStorage storage) =>
    storage.GetAllProducts());

#endregion

app.MapPost("product-shop-stream/{productId:long}",
    async (EventStoreClient client,
        long productId,
        ProductAddedToShop @event,
        CancellationToken cancellationToken) =>
{
    //sub from shops
    var eventData = new EventData(
        Uuid.NewUuid(),
        "productAddedToShop",
        JsonSerializer.SerializeToUtf8Bytes(@event));

    var result = await client.AppendToStreamAsync(
        $"{productId}-{@event.ShopChainId}",
        StreamState.NoStream,
        new[] { eventData },
        cancellationToken: cancellationToken);
    
    return Results.Ok(result);
});

app.MapPut("product-shop-price/{productId:long}/{shopChainId:long}",
    async (EventStoreClient client,
        long productId,
        long shopChainId,
        ProductPriceChanged @event,
        CancellationToken cancellationToken) =>
    {
        //sub from shops
        var eventData = new EventData(
            Uuid.NewUuid(),
            "productPriceChanged",
            JsonSerializer.SerializeToUtf8Bytes(@event));

        var result = await client.AppendToStreamAsync(
            $"{productId}-{shopChainId}",
            StreamState.StreamExists,
            new[] { eventData },
            cancellationToken: cancellationToken);
    
        return Results.Ok(result);
    });

app.MapGet("product/{productId:long}/{shopChainId:long}",
    async (EventStoreClient store,
        long shopChainId,
        long productId,
        CancellationToken cancellationToken) =>
{
    var product = store.ReadStreamAsync(Direction.Forwards,
        $"{productId}-{shopChainId}",
        StreamPosition.Start,
        cancellationToken: cancellationToken);

    await foreach (var message in product.Messages)
    {
        Console.WriteLine(message);
    }
    return Results.Ok(product);
});


app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Playground"); });
app.Run();