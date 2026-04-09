using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// Activamos OpenAPI y CORS para que el frontend pueda llamar al backend
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Leemos la configuración de Cosmos
var connectionString = builder.Configuration.GetConnectionString("CosmosDb");
var dbName = builder.Configuration["CosmosDbSettings:DatabaseName"];
var containerName = builder.Configuration["CosmosDbSettings:ContainerName"];

// Registramos el cliente de Cosmos como singleton
builder.Services.AddSingleton(_ => new CosmosClient(connectionString));

var app = builder.Build();

app.UseCors();
app.MapOpenApi();
app.UseHttpsRedirection();

app.MapGet("/", () => "¡Backend de cartas conectado y listo!");

// ======================================================
// GET /cartas
// Devuelve todas las cartas guardadas
// ======================================================
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

// ======================================================
// GET /cartas/{id}
// Devuelve una carta concreta por id
// ======================================================
app.MapGet("/cartas/{id}", async (CosmosClient client, string id) =>
{
    try
    {
        var container = client.GetContainer(dbName, containerName);
        var response = await container.ReadItemAsync<Carta>(id, new PartitionKey(id));

        return Results.Ok(response.Resource);
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return Results.NotFound(new { mensaje = $"No existe una carta con id {id}." });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al obtener la carta: {ex.Message}");
    }
});

// ======================================================
// POST /cartas
// Crea una carta nueva
// ======================================================
app.MapPost("/cartas", async (CosmosClient client, CartaCrearRequest nuevaCarta) =>
{
    try
    {
        var error = ValidarCarta(nuevaCarta);
        if (error != null)
        {
            return Results.BadRequest(new { mensaje = error });
        }

        var container = client.GetContainer(dbName, containerName);

        // Buscamos el id numérico más alto para generar el siguiente
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

        var carta = ConstruirCarta(siguienteId, nuevaCarta);

        await container.CreateItemAsync(carta, new PartitionKey(carta.id));

        return Results.Created($"/cartas/{carta.id}", carta);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al crear carta: {ex.Message}");
    }
});

// ======================================================
// PUT /cartas/{id}
// Actualiza una carta existente
// ======================================================
app.MapPut("/cartas/{id}", async (CosmosClient client, string id, CartaCrearRequest cartaActualizada) =>
{
    try
    {
        var error = ValidarCarta(cartaActualizada);
        if (error != null)
        {
            return Results.BadRequest(new { mensaje = error });
        }

        var container = client.GetContainer(dbName, containerName);

        // Comprobamos que la carta exista antes de reemplazarla
        await container.ReadItemAsync<Carta>(id, new PartitionKey(id));

        var carta = ConstruirCarta(id, cartaActualizada);

        // ReplaceItemAsync sustituye el documento completo
        var response = await container.ReplaceItemAsync(carta, id, new PartitionKey(id));

        return Results.Ok(response.Resource);
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return Results.NotFound(new { mensaje = $"No existe una carta con id {id}." });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al actualizar carta: {ex.Message}");
    }
});

// ======================================================
// DELETE /cartas/{id}
// Elimina una carta
// ======================================================
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

// ======================================================
// FUNCIÓN DE VALIDACIÓN
// Devuelve null si todo está bien
// Devuelve un mensaje si hay error
// ======================================================
static string? ValidarCarta(CartaCrearRequest carta)
{
    var opcionesArma = new[] { "Kette", "Stab", "Corredor", "Qtip", "Duales", "SNS", "Mandoble" };
    var tipoNormalizado = carta.tipo?.Trim().ToLower();

    if (tipoNormalizado != "arma" && tipoNormalizado != "jugador")
    {
        return "El tipo solo puede ser 'arma' o 'jugador'.";
    }

    if (string.IsNullOrWhiteSpace(carta.nombre))
    {
        return "El nombre es obligatorio.";
    }

    if (string.IsNullOrWhiteSpace(carta.descripcion))
    {
        return "La descripción es obligatoria.";
    }

    if (tipoNormalizado == "jugador")
    {
        var equipoNormalizado = carta.equipo?.Trim().ToLower();

        if (string.IsNullOrWhiteSpace(carta.arma) || !opcionesArma.Contains(carta.arma))
        {
            return "El arma del jugador no es válida.";
        }

        if (carta.poder == null)
        {
            return "La carta de tipo jugador debe tener poder.";
        }
    }
    else
    {
        if (string.IsNullOrWhiteSpace(carta.tipoArma) || !opcionesArma.Contains(carta.tipoArma))
        {
            return "El tipo del arma no es válido.";
        }

        if (carta.bonificador == null)
        {
            return "La carta de tipo arma debe tener bonificador.";
        }
    }

    return null;
}

// ======================================================
// FUNCIÓN QUE CONSTRUYE LA CARTA FINAL
// Según el tipo, rellena unos campos y deja otros en null
// ======================================================
static Carta ConstruirCarta(string id, CartaCrearRequest datos)
{
    var tipoNormalizado = datos.tipo.Trim().ToLower();

    if (tipoNormalizado == "jugador")
    {
        return new Carta(
            id,
            tipoNormalizado,
            datos.nombre.Trim(),
            datos.equipo!.Trim().ToLower(),
            datos.poder,
            datos.arma,
            null,
            null,
            datos.descripcion.Trim(),
            datos.imagen
        );
    }

    return new Carta(
        id,
        tipoNormalizado,
        datos.nombre.Trim(),
        null,
        null,
        null,
        datos.tipoArma,
        datos.bonificador,
        datos.descripcion.Trim(),
        datos.imagen
    );
}

// Modelo que llega desde el frontend
public record CartaCrearRequest(
    string tipo,
    string nombre,
    string? equipo,
    int? poder,
    string? arma,
    string? tipoArma,
    int? bonificador,
    string descripcion,
    string? imagen
);

// Modelo final que se guarda en la base de datos
public record Carta(
    string id,
    string tipo,
    string nombre,
    string? equipo,
    int? poder,
    string? arma,
    string? tipoArma,
    int? bonificador,
    string descripcion,
    string? imagen
);