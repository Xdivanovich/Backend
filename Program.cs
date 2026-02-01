using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURACIÓN DE SERVICIOS ---
builder.Services.AddOpenApi();

builder.Services.AddCors(options => {
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Leemos las etiquetas de configuración
var connectionString = builder.Configuration.GetConnectionString("CosmosDb");
var dbName = builder.Configuration["CosmosDbSettings:DatabaseName"];
var containerName = builder.Configuration["CosmosDbSettings:ContainerName"];

// Registramos el cliente de Cosmos
builder.Services.AddSingleton(s => new CosmosClient(connectionString));

var app = builder.Build();

// --- 2. CONFIGURACIÓN DE LA APP ---
app.UseCors();
app.MapOpenApi();
app.UseHttpsRedirection();

app.MapGet("/", () => "¡Backend conectado y listo!");

// --- Endpoint de DIAGNÓSTICO (Reemplaza al original) ---
app.MapGet("/usuarios", async () =>
{
    try
    {
        // 1. Verificación manual de variables
        if (string.IsNullOrEmpty(connectionString)) 
            throw new Exception("La cadena de conexión (ConnectionStrings:CosmosDb) es NULL o vacía.");
        if (string.IsNullOrEmpty(dbName)) 
            throw new Exception("El nombre de la DB (CosmosDbSettings:DatabaseName) es NULL o vacío.");
        if (string.IsNullOrEmpty(containerName)) 
            throw new Exception("El nombre del contenedor (CosmosDbSettings:ContainerName) es NULL o vacío.");

        // 2. Conexión Manual (para descartar fallos de inyección)
        var debugClient = new CosmosClient(connectionString);
        var container = debugClient.GetContainer(dbName, containerName);

        // 3. Intento de lectura
        var query = new QueryDefinition("SELECT * FROM c");
        var iterator = container.GetItemQueryIterator<Usuario>(query);
        var resultados = new List<Usuario>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            resultados.AddRange(response);
        }
        return Results.Ok(resultados);
    }
    catch (Exception ex)
    {
        // Forzamos el código 200 (OK) para que el navegador muestre el texto del error
        return Results.Ok($"🔴 ERROR DETECTADO: {ex.ToString()}");
    }
});

// Endpoint para CREAR (POST) - Lo mantengo activo para cuando arreglemos el GET
app.MapPost("/usuarios", async (CosmosClient client, Usuario nuevoUsuario) =>
{
    var container = client.GetContainer(dbName, containerName);
    await container.CreateItemAsync(nuevoUsuario, new PartitionKey(nuevoUsuario.id));
    return Results.Created($"/usuarios/{nuevoUsuario.id}", nuevoUsuario);
});

app.Run();

// Modelo de datos
public record Usuario(string id, string nombre);
