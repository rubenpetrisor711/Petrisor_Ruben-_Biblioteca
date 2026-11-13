using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Lab1_MVC.Data;
using Microsoft.Extensions.ML;
using Lab1_MVC.Models;
using Lab1_MVC.Api;
using Scalar.AspNetCore;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using Lab1_MVC.Services;
using OllamaSharp;
using BibliotecaGrpc;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. CONFIGURARE SERVICII (DI)
// ==========================================

builder.Services.AddDbContext<LibraryContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("LibraryContext")
        ?? throw new InvalidOperationException("Connection string 'LibraryContext' not found.")));

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services
    .AddPredictionEnginePool<BookRating, BookRatingPrediction>()
    .FromFile(modelName: "BookRecommender",
        filePath: "MLModels/BookRecommender.zip",
        watchForChanges: true);

builder.Services.AddValidation();
builder.Services.AddGrpc();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddOpenApi();

// Configurare Client AI (Ollama prin OpenAI Client Compatibility)
var endpoint = new Uri("http://localhost:11434/v1");
IChatClient chat = new OpenAIClient(new ApiKeyCredential("ollama"),
        new OpenAIClientOptions { Endpoint = endpoint })
    .GetChatClient("gemma3:4b")
    .AsIChatClient();

builder.Services.AddChatClient(chat);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// ===== RAG (căutare după sens) =====
// generatorul de embeddings: transformă un text într-un vector, cu modelul embeddinggemma din Ollama
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    new OllamaApiClient(new Uri("http://localhost:11434"), "embeddinggemma"));

// indexul cu regulamentul și descrierile cărților (un singur exemplar, ținut în memorie)
builder.Services.AddSingleton<DocumentIndex>();

// Configurare clienți gRPC externi
builder.Services.AddGrpcClient<Recommender.RecommenderClient>(o =>
{
    o.Address = new Uri(builder.Configuration["Biblioteca:GrpcAddress"]
        ?? throw new InvalidOperationException("Configurarea 'Biblioteca:GrpcAddress' lipsește."));
});

builder.Services.AddGrpcClient<ReaderRatings.ReaderRatingsClient>(o =>
{
    o.Address = new Uri(builder.Configuration["Biblioteca:GrpcAddress"]
        ?? throw new InvalidOperationException("Configurarea 'Biblioteca:GrpcAddress' lipsește."));
});

var app = builder.Build();

builder.Services.AddSingleton<IChatClient>(
    new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2"));

// ==========================================
// 2. INITIALIZARE BAZA DE DATE & RAG INDEX
// ==========================================

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryContext>();
    db.Database.Migrate();
    LibrarySeed.Seed(db);
    LibrarySeed.SeedRatings(db);
}

// Construim indexul o singură dată, la pornire.
// Trebuie după Migrate/Seed, ca să existe cărțile în baza de date.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryContext>();
    var index = scope.ServiceProvider.GetRequiredService<DocumentIndex>();
    await index.BuildAsync(db, Path.Combine(app.Environment.ContentRootPath, "Docs", "regulament.md"));
}

// ==========================================
// 3. PIPELINE-UL HTTP (MIDDLEWARE)
// ==========================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
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

app.MapLibraryApi();
app.MapGrpcService<RecommenderGrpcService>();
app.MapGrpcService<ReaderRatingsGrpcService>();
app.MapMcp("/mcp");

// Același serviciu de recomandare, expus și prin REST (pentru comparație cu gRPC)
app.MapGet("/api/v2/readers/{id:int}/recommendations",
    async (int id, int? top, RecommendationService recs, CancellationToken ct) =>
        Results.Ok(await recs.RecommendAsync(id, top ?? 5, ct)));

app.MapPost("/api/v2/books/{id:int}/pitch", async (int id, LibraryContext db, IChatClient ai) =>
{
    var b = await db.Book.Include(x => x.Author).Include(x => x.Genre)
        .FirstOrDefaultAsync(x => x.ID == id);
    if (b is null) return Results.NotFound();

    List<ChatMessage> messages =
    [
        new(ChatRole.System, "Ești bibliotecar. Răspunde în română, în cel mult 2 propoziții."),
        new(ChatRole.User, $"Recomandă „{b.Title}” de {b.Author?.FullName} " +
                           $"({b.Genre?.Name}). Descriere: {b.Description}")
    ];

    ChatResponse r = await ai.GetResponseAsync(messages);
    return Results.Ok(new { b.Title, Pitch = r.Text });
});

app.MapGet("/api/v2/ask", async (string q, IChatClient ai) =>
    (await ai.GetResponseAsync(q)).Text);

// Rute MVC
app.MapControllerRoute(
    name: "start",
    pattern: "start",
    defaults: new { controller = "Home", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();