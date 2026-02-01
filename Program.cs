using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURACIÓN DE SERVICIOS ---
builder.Services.AddOpenApi();

// Añadimos CORS para que tu Frontend (Static Web App) pueda conectarse
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Leemos las etiquetas CORRECTAS del appsettings.json
var connectionString = builder.Configuration.GetConnectionString("CosmosDb");
var dbName = builder.Configuration["CosmosDbSettings:DatabaseName"]; // ANTES DECÍA "DBcomputacion"
var containerName = builder.Configuration["CosmosDbSettings:ContainerName"]; // ANTES DECÍA "Usuarios"

// Registramos el cliente de Cosmos
builder.Services.AddSingleton(s => new CosmosClient(connectionString));

var app = builder.Build();

// --- 2. CONFIGURACIÓN DE LA APP ---
app.UseCors(); // Activamos el permiso para el Frontend
app.MapOpenApi();
app.UseHttpsRedirection();

// Mensaje de bienvenida
app.MapGet("/", () => "¡Backend conectado y listo!");
/*
// Endpoint para CREAR (POST)
app.MapPost("/usuarios", async (CosmosClient client, Usuario nuevoUsuario) =>
{
    var container = client.GetContainer(dbName, containerName);
    await container.CreateItemAsync(nuevoUsuario, new PartitionKey(nuevoUsuario.id));
    return Results.Created($"/usuarios/{nuevoUsuario.id}", nuevoUsuario);
});
*/

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
        // AQUÍ ESTÁ LA CLAVE: Devolvemos el error exacto al navegador
        return Results.Problem($"ERROR REAL: {ex.Message} ||| TIPO: {ex.GetType().Name} ||| CAMINO: {ex.StackTrace}");
    }
});

// Endpoint para CONSULTAR (GET)
app.MapGet("/usuarios", async (CosmosClient client) =>
{
    var container = client.GetContainer(dbName, containerName);
    var query = new QueryDefinition("SELECT * FROM c");
    var iterator = container.GetItemQueryIterator<Usuario>(query);
    var resultados = new List<Usuario>();

    while (iterator.HasMoreResults)
    {
        var response = await iterator.ReadNextAsync();
        resultados.AddRange(response);
    }
    return Results.Ok(resultados);
});

app.Run();

// Modelo de datos
public record Usuario(string id, string nombre);
