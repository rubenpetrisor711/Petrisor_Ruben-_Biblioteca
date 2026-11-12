using BibliotecaGrpc;
using Lab1_MVC.Api;
using Lab1_MVC.Data;
using Lab1_MVC.Models;
using Lab1_MVC.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.ML;
using OllamaSharp;
using OpenAI;
using Scalar.AspNetCore;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. CONFIGURARE SERVICII (DI)
// ==========================================

// Baza de date SQLite
builder.Services.AddDbContext<LibraryContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("LibraryContext")
        ?? throw new InvalidOperationException("Connection string 'LibraryContext' not found.")));

// MVC & API-uri
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();

// Machine Learning (ML.NET Prediction Engine)
builder.Services
    .AddPredictionEnginePool<BookRating, BookRatingPrediction>()
    .FromFile(
        modelName: "BookRecommender",
        filePath: "MLModels/BookRecommender.zip",
        watchForChanges: true);

// Validare și gRPC
builder.Services.AddValidation();
builder.Services.AddGrpc();

// Servicii aplicație
builder.Services.AddScoped<RecommendationService>();

// Configurare Client AI (Ollama prin OpenAI Client Compatibility)
var ollamaEndpoint = new Uri("http://localhost:11434/v1");
IChatClient chatClient = new OpenAIClient(
        new ApiKeyCredential("ollama"),
        new OpenAIClientOptions { Endpoint = ollamaEndpoint })
    .GetChatClient("gemma3:4b")
    .AsIChatClient();

builder.Services.AddChatClient(chatClient);

// Model de generare Embeddings (pentru RAG / căutare după sens)
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    new OllamaApiClient(new Uri("http://localhost:11434"), "embeddinggemma"));

// Indexul în memorie pentru RAG (regulament + descrieri cărți)
builder.Services.AddSingleton<DocumentIndex>();

// Model Context Protocol (MCP)
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Client gRPC extern (pentru comunicarea cu serviciul de recomandări extern)
builder.Services.AddGrpcClient<Recommender.RecommenderClient>(o =>
{
    o.Address = new Uri(builder.Configuration["Biblioteca:GrpcAddress"]
        ?? throw new InvalidOperationException("Configurarea 'Biblioteca:GrpcAddress' lipsește."));
});

var app = builder.Build();

// ==========================================
// 2. INITIALIZARE BAZA DE DATE & RAG INDEX
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<LibraryContext>();

    // Aplică migrările și rulează seed-ul
    db.Database.Migrate();
    LibrarySeed.Seed(db);
    LibrarySeed.SeedRatings(db);

    // Construiește indexul RAG după ce datele sunt în baza de date
    var index = services.GetRequiredService<DocumentIndex>();
    await index.BuildAsync(db, Path.Combine(app.Environment.ContentRootPath, "Docs", "regulament.md"));
}

// ==========================================
// 3. PIPELINE-UL HTTP (MIDDLEWARE)
// ==========================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    // Scalar & OpenAPI disponibile în mod de dezvoltare
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// ==========================================
// 4. MAPARE RUTE ȘI ENDPOINT-URI
// ==========================================

// Endpoint-uri Minimal APIs și gRPC proprii
app.MapLibraryApi();
app.MapGrpcService<RecommenderGrpcService>();
app.MapMcp("/mcp");

// REST Endpoints suplimentare
app.MapGet("/api/v2/readers/{id:int}/recommendations",
    async (int id, int? top, RecommendationService recs, CancellationToken ct) =>
        Results.Ok(await recs.RecommendAsync(id, top ?? 5, ct)));

app.MapPost("/api/v2/books/{id:int}/pitch", async (int id, LibraryContext db, IChatClient ai) =>
{
    var b = await db.Book
        .Include(x => x.Author)
        .Include(x => x.Genre)
        .FirstOrDefaultAsync(x => x.ID == id);

    if (b is null) return Results.NotFound();

    List<ChatMessage> messages =
    [
        new(ChatRole.System, "Ești bibliotecar. Răspunde în română, în cel mult 2 propoziții."),
        new(ChatRole.User, $"Recomandă „{b.Title}” de {b.Author?.FullName} ({b.Genre?.Name}). Descriere: {b.Description}")
    ];

    ChatResponse r = await ai.GetResponseAsync(messages);
    return Results.Ok(new { b.Title, Pitch = r.Text });
});

app.MapGet("/api/v2/ask", async (string q, IChatClient ai) =>
    (await ai.GetResponseAsync(q)).Text);

// Rute MVC traditionale
app.MapControllerRoute(
    name: "start",
    pattern: "start",
    defaults: new { controller = "Home", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();