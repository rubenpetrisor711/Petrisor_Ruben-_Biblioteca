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

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<LibraryContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("LibraryContext") ?? throw new InvalidOperationException("Connection string 'LibraryContext' not found.")));

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

var endpoint = new Uri("http://localhost:11434/v1");

IChatClient chat = new OpenAIClient(new ApiKeyCredential("ollama"),
        new OpenAIClientOptions { Endpoint = endpoint })
    .GetChatClient("gemma3:4b")
    .AsIChatClient();

builder.Services.AddChatClient(chat);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// ===== Î8: RAG (cautare dupa sens) =====
// generatorul de embeddings: transforma un text intr-un vector, cu modelul embeddinggemma din Ollama
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    new OllamaApiClient(new Uri("http://localhost:11434"), "embeddinggemma"));
// indexul cu regulamentul si descrierile cartilor (un singur exemplar, tinut in memorie)
builder.Services.AddSingleton<DocumentIndex>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapLibraryApi();
app.MapGrpcService<RecommenderGrpcService>();
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
        new(ChatRole.System, "Esti bibliotecar. Raspunde in romana, in cel mult 2 propozitii."),
        new(ChatRole.User, $"Recomanda „{b.Title}” de {b.Author?.FullName} " +
                           $"({b.Genre?.Name}). Descriere: {b.Description}")
    ];

    ChatResponse r = await ai.GetResponseAsync(messages);
    return Results.Ok(new { b.Title, Pitch = r.Text });
});

app.MapGet("/api/v2/ask", async (string q, IChatClient ai) =>
    (await ai.GetResponseAsync(q)).Text);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryContext>();
    db.Database.Migrate();
    LibrarySeed.Seed(db);
    LibrarySeed.SeedRatings(db);
}

// Construim indexul o singura data, la pornire (cateva secunde).
// Trebuie dupa Migrate/Seed, ca sa existe cartile in baza de date.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryContext>();
    var index = app.Services.GetRequiredService<DocumentIndex>();
    await index.BuildAsync(db, Path.Combine(app.Environment.ContentRootPath, "Docs", "regulament.md"));
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "start",
    pattern: "start",
    defaults: new { controller = "Home", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();