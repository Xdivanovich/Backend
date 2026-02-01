using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// --- SERVICIOS ---
builder.Services.AddOpenApi();
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var connectionString = builder.Configuration.GetConnectionString("CosmosDb");
var dbName = builder.Configuration["CosmosDbSettings:DatabaseName"];
var containerName = builder.Configuration["CosmosDbSettings:ContainerName"];

builder.Services.AddSingleton(s => new CosmosClient(connectionString));

var app = builder.Build();

// --- MIDDLEWARE ---
app.UseCors();
app.MapOpenApi();
app.UseHttpsRedirection();

app.MapGet("/", () => "¡El Backend está funcionando correctamente y conectado a Cosmos DB!");

// ENDPOINT: LISTAR
app.MapGet("/usuarios", async (CosmosClient client) =>
{
    var container = client.GetContainer(dbName, containerName);
    var iterator = container.GetItemQueryIterator<Usuario>("SELECT * FROM c");
    var resultados = new List<Usuario>();
    while (iterator.HasMoreResults) {
        var response = await iterator.ReadNextAsync();
        resultados.AddRange(response);
    }
    return Results.Ok(resultados);
});

// ENDPOINT: CREAR
app.MapPost("/usuarios", async (CosmosClient client, Usuario nuevoUsuario) =>
{
    var container = client.GetContainer(dbName, containerName);
    await container.CreateItemAsync(nuevoUsuario, new PartitionKey(nuevoUsuario.id));
    return Results.Created($"/usuarios/{nuevoUsuario.id}", nuevoUsuario);
});

app.Run();

public record Usuario(string id, string nombre);
