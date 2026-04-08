using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var connectionString = builder.Configuration.GetConnectionString("CosmosDb");
var dbName = builder.Configuration["CosmosDbSettings:DatabaseName"];
var containerName = builder.Configuration["CosmosDbSettings:ContainerName"];

builder.Services.AddSingleton(_ => new CosmosClient(connectionString));

var app = builder.Build();

app.UseCors();
app.MapOpenApi();
app.UseHttpsRedirection();

app.MapGet("/", () => "¡Backend de cartas conectado y listo!");

// GET /cartas
app.MapGet("/cartas", async (CosmosClient client) =>
{
    try
    {
        var container = client.GetContainer(dbName, containerName);

        var query = new QueryDefinition("SELECT * FROM c");
        var iterator = container.GetItemQueryIterator<Carta>(query);

        var resultados = new List<Carta>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            resultados.AddRange(response);
        }

        return Results.Ok(resultados.OrderBy(c => c.nombre));
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al obtener cartas: {ex.Message}");
    }
});

// POST /cartas
app.MapPost("/cartas", async (CosmosClient client, CartaCrearRequest nuevaCarta) =>
{
    try
    {
        var tipoNormalizado = nuevaCarta.tipo?.Trim().ToLower();
        var equipoNormalizado = nuevaCarta.equipo?.Trim().ToLower();

        if (tipoNormalizado != "arma" && tipoNormalizado != "jugador")
        {
            return Results.BadRequest(new
            {
                mensaje = "El tipo solo puede ser 'arma' o 'jugador'."
            });
        }

        if (equipoNormalizado != "amantes" && equipoNormalizado != "botillo")
        {
            return Results.BadRequest(new
            {
                mensaje = "El equipo solo puede ser 'amantes' o 'botillo'."
            });
        }

        if (string.IsNullOrWhiteSpace(nuevaCarta.nombre))
        {
            return Results.BadRequest(new
            {
                mensaje = "El nombre es obligatorio."
            });
        }

        if (string.IsNullOrWhiteSpace(nuevaCarta.descripcion))
        {
            return Results.BadRequest(new
            {
                mensaje = "La descripción es obligatoria."
            });
        }

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

        var carta = new Carta(
            siguienteId,
            tipoNormalizado,
            nuevaCarta.nombre.Trim(),
            equipoNormalizado,
            nuevaCarta.poder,
            nuevaCarta.descripcion.Trim()
        );

        await container.CreateItemAsync(carta, new PartitionKey(carta.id));

        return Results.Created($"/cartas/{carta.id}", carta);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al crear carta: {ex.Message}");
    }
});

// DELETE /cartas/{id}
app.MapDelete("/cartas/{id}", async (CosmosClient client, string id) =>
{
    try
    {
        var container = client.GetContainer(dbName, containerName);

        await container.DeleteItemAsync<Carta>(id, new PartitionKey(id));

        return Results.Ok(new
        {
            mensaje = $"Carta con id {id} eliminada correctamente."
        });
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return Results.NotFound(new
        {
            mensaje = $"No existe una carta con id {id}."
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al eliminar carta: {ex.Message}");
    }
});

app.Run();

public record CartaCrearRequest(
    string tipo,
    string nombre,
    string equipo,
    int poder,
    string descripcion
);

public record Carta(
    string id,
    string tipo,
    string nombre,
    string equipo,
    int poder,
    string descripcion
);