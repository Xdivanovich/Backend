using Microsoft.Azure.Cosmos; // IMPORTANTE: Asegúrate de que esta línea esté arriba

var builder = WebApplication.CreateBuilder(args);

// --- SECCIÓN DE SERVICIOS (Aquí agregas cosas al builder) ---
builder.Services.AddOpenApi();

// 1. Configurar el Cliente de Cosmos DB (Copia y pega esto aquí)
var connectionString = builder.Configuration.GetConnectionString("CosmosDb");
var dbName = builder.Configuration["CosmosDbSettings:DatabaseName"];
var containerName = builder.Configuration["CosmosDbSettings:ContainerName"];

// Registramos el cliente para poder usarlo en los endpoints
builder.Services.AddSingleton(s => new CosmosClient(connectionString));

var app = builder.Build();

// --- SECCIÓN DE CONFIGURACIÓN DE LA APP (Después del build) ---

app.MapOpenApi(); 

app.MapGet("/", () => "¡El Backend está funcionando correctamente y conectado a Cosmos DB!");

app.UseHttpsRedirection();

// Ejemplo de cómo guardar algo en Cosmos DB (puedes borrarlo luego si no lo usas)
app.MapPost("/usuarios", async (CosmosClient client, Usuario nuevoUsuario) =>
{
    var container = client.GetContainer(dbName, containerName);
    await container.CreateItemAsync(nuevoUsuario, new PartitionKey(nuevoUsuario.id));
    return Results.Created($"/usuarios/{nuevoUsuario.id}", nuevoUsuario);
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

// --- TUS MODELOS/RECORDS (Al final del archivo) ---

public record Usuario(string id, string nombre, string email);

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}