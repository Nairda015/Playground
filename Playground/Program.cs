using System.Text.Json;
using EventStore.Client;
using Playground;
using static Playground.IdGen;

var builder = WebApplication.CreateBuilder(args);

//Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => { c.OrderActionsBy(x => x.HttpMethod); });

builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<InMemoryDocumentStorage>();
builder.Services.AddEventStoreClient("esdb://localhost:2113?tls=false", x => 
{
    
});


var app = builder.Build();


AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
app.UseSwagger();
app.UseHttpsRedirection();
app.UseRouting();

app.MapGet("/api/health", () => "Playground is healthy");

app.MapPost("product/{name}",  (InMemoryDocumentStorage storage, string name) =>
{
    //sub from products
    var prodId = RandomLong();
    var product = new Product(prodId, name, "TestCategory");
    storage.Products.Add(prodId, product);
    return Results.Created("product/{productId:long}", product);
});

app.MapGet("product/{productId:long}", (InMemoryDocumentStorage storage, long productId) =>
        storage.Products.TryGetValue(productId, out var product)
        ? Results.Ok(product)
        : Results.NotFound());

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

app.MapPut("product-shop-price/{productId:long}/{shopChainId:long}/{shopId:long}/{price:decimal}",
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