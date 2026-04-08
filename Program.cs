using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURACIÓN DE SERVICIOS ---
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var connectionString = builder.Configuration.GetConnectionString("CosmosDb");
var dbName = builder.Configuration["CosmosDbSettings:DatabaseName"];
var containerName = builder.Configuration["CosmosDbSettings:ContainerName"];

builder.Services.AddSingleton(s => new CosmosClient(connectionString));

var app = builder.Build();

// --- 2. CONFIGURACIÓN DE LA APP ---
app.UseCors();
app.MapOpenApi();
app.UseHttpsRedirection();

app.MapGet("/", () => "¡Backend conectado y listo!");

// GET /equipos
app.MapGet("/equipos", async (CosmosClient client) =>
{
    try
    {
        var container = client.GetContainer(dbName, containerName);

        var query = new QueryDefinition("SELECT * FROM c");
        var iterator = container.GetItemQueryIterator<Equipo>(query);

        var resultados = new List<Equipo>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            resultados.AddRange(response);
        }

        return Results.Ok(resultados.OrderByDescending(e => e.puntos));
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al obtener equipos: {ex.Message}");
    }
});

// POST /equipos
app.MapPost("/equipos", async (CosmosClient client, EquipoCrearRequest nuevoEquipo) =>
{
    try
    {
        var container = client.GetContainer(dbName, containerName);

        var query = new QueryDefinition("SELECT c.id FROM c");
        var iterator = container.GetItemQueryIterator<dynamic>(query);

        int maxId = 0;

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();

            foreach (var item in response)
            {
                if (int.TryParse((string)item.id, out int idNumerico) && idNumerico > maxId)
                {
                    maxId = idNumerico;
                }
            }
        }

        var siguienteId = (maxId + 1).ToString();

        var equipo = new Equipo(
            siguienteId,
            nuevoEquipo.nombre,
            nuevoEquipo.jugadores,
            nuevoEquipo.puntos
        );

        await container.CreateItemAsync(equipo, new PartitionKey(equipo.id));

        return Results.Created($"/equipos/{equipo.id}", equipo);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al crear equipo: {ex.Message}");
    }
});

// DELETE /equipos/{id}
app.MapDelete("/equipos/{id}", async (CosmosClient client, string id) =>
{
    try
    {
        var container = client.GetContainer(dbName, containerName);

        await container.DeleteItemAsync<Equipo>(id, new PartitionKey(id));

        return Results.Ok(new { mensaje = $"Equipo {id} eliminado correctamente." });
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return Results.NotFound(new { mensaje = $"No existe un equipo con id {id}." });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al eliminar equipo: {ex.Message}");
    }
});

app.Run();

// Modelo para crear equipos desde el frontend
public record EquipoCrearRequest(string nombre, List<string> jugadores, int puntos);

// Modelo guardado en Cosmos
public record Equipo(string id, string nombre, List<string> jugadores, int puntos);