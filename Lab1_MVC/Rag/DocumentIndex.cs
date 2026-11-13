using CommunityToolkit.VectorData.InMemory;
using Lab1_MVC.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

public class DocumentIndex
{
 
    public const string ChunkMode = "sectiuni";        

    private readonly VectorStoreCollection<string, DocumentChunk> _collection;
    private readonly ILogger<DocumentIndex> _log;

    public DocumentIndex(IEmbeddingGenerator<string, Embedding<float>> embeddings, ILogger<DocumentIndex> log)
    {
        _log = log;
        var store = new InMemoryVectorStore(new() { EmbeddingGenerator = embeddings });
        _collection = store.GetCollection<string, DocumentChunk>("biblioteca");
    }

    public int Count { get; private set; }

    public async Task BuildAsync(LibraryContext db, string regulamentPath, CancellationToken ct = default)
    {
        await _collection.EnsureCollectionExistsAsync(ct);

       
        var parts = Chunker.Split(await File.ReadAllTextAsync(regulamentPath, ct), ChunkMode);
        for (int i = 0; i < parts.Count; i++)
        {
            await _collection.UpsertAsync(new DocumentChunk
            {
                Id = $"regulament-{i + 1:00}",
                Source = "regulament",
                Title = parts[i].Title,
                Text = parts[i].Text,
                Embedding = parts[i].Text
            }, ct);
        }

        
        var books = await db.Book.AsNoTracking()
            .Where(b => b.Description != null)
            .Select(b => new
            {
                b.ID,
                b.Title,
                b.Description,
                Author = b.Author == null ? "" : b.Author.FirstName + " " + b.Author.LastName,
                Genre = b.Genre == null ? "" : b.Genre.Name
            })
            .ToListAsync(ct);

        foreach (var b in books)
        {
            var text = $"{b.Title}, de {b.Author} ({b.Genre}). {b.Description}";
            await _collection.UpsertAsync(new DocumentChunk
            {
                Id = $"carte-{b.ID}",
                Source = "carte",
                Title = $"{b.Title} (ID {b.ID})",
                Text = text,
                Embedding = text
            }, ct);
        }

        Count = parts.Count + books.Count;
        _log.LogInformation("RAG: {Parts} fragmente din regulament ({Mode}) + {Books} carti indexate.",
            parts.Count, ChunkMode, books.Count);
    }

    public async Task<List<DocumentHit>> SearchAsync(string query, int top, string? source, CancellationToken ct = default)
    {
        var options = source is null
            ? null
            : new VectorSearchOptions<DocumentChunk> { Filter = c => c.Source == source };

        var hits = new List<DocumentHit>();
        await foreach (var r in _collection.SearchAsync(query, top, options, ct))
            hits.Add(new DocumentHit(r.Record.Source, r.Record.Title, r.Record.Text,
                                     Math.Round(r.Score ?? 0, 3)));
        return hits;
    }


    public async Task IndexBookAsync(LibraryContext db, int bookId, CancellationToken ct = default)
    {
        var b = await db.Book.AsNoTracking()
            .Where(x => x.ID == bookId)
            .Select(x => new
            {
                x.ID,
                x.Title,
                x.Description,
                Author = x.Author == null ? "" : x.Author.FirstName + " " + x.Author.LastName,
                Genre = x.Genre == null ? "" : x.Genre.Name
            })
            .FirstOrDefaultAsync(ct);

        if (b is null || string.IsNullOrWhiteSpace(b.Description))
        {
            await RemoveBookAsync(bookId, ct);   
            return;
        }

        var text = $"{b.Title}, de {b.Author} ({b.Genre}). {b.Description}";
        await _collection.UpsertAsync(new DocumentChunk
        {
            Id = $"carte-{b.ID}",                
            Source = "carte",
            Title = $"{b.Title} (ID {b.ID})",
            Text = text,
            Embedding = text
        }, ct);
    }

  
    public Task RemoveBookAsync(int bookId, CancellationToken ct = default)
        => _collection.DeleteAsync($"carte-{bookId}", ct);
}