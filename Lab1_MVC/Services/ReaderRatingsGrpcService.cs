using BibliotecaGrpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lab1_MVC.Data;
using Lab1_MVC.Models;
using Microsoft.EntityFrameworkCore;

public class ReaderRatingsGrpcService(LibraryContext db)
    : ReaderRatings.ReaderRatingsBase
{
    private static void ValidateReader(int readerId)
    {
        if (readerId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "reader_id trebuie să fie un număr pozitiv."));
    }

    private static void ValidateStars(int stars)
    {
        if (stars < 1 || stars > 5)
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "Nota trebuie să fie între 1 și 5."));
    }

    private static RpcException RatingNotFound(int id) =>
        new(new Status(StatusCode.NotFound, $"Evaluarea cu id-ul {id} nu există."));

    private static RatingItem ToItem(Rating r, string title) => new()
    {
        Id = r.Id,
        BookId = r.BookId,
        Title = title,
        Stars = r.Stars,
        CreatedAt = Timestamp.FromDateTime(
            DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc))
    };

    public override async Task ListRatings(ReaderRequest request,
        IServerStreamWriter<RatingItem> responseStream, ServerCallContext context)
    {
        ValidateReader(request.ReaderId);
        var ct = context.CancellationToken;

        var rows = db.Ratings.AsNoTracking()
            .Where(r => r.ReaderId == request.ReaderId)
            .OrderBy(r => r.Id)
            .Select(r => new { Rating = r, Title = r.Book.Title })
            .AsAsyncEnumerable();

        await foreach (var x in rows.WithCancellation(ct))
            await responseStream.WriteAsync(ToItem(x.Rating, x.Title), ct);
    }

    public override async Task<RatingItem> GetRating(RatingId request,
        ServerCallContext context)
    {
        var x = await db.Ratings.AsNoTracking()
            .Where(r => r.Id == request.Id)
            .Select(r => new { Rating = r, Title = r.Book.Title })
            .FirstOrDefaultAsync(context.CancellationToken)
            ?? throw RatingNotFound(request.Id);
        return ToItem(x.Rating, x.Title);
    }

    public override async Task<RatingItem> AddRating(NewRating request,
        ServerCallContext context)
    {
        ValidateReader(request.ReaderId);
        ValidateStars(request.Stars);
        var ct = context.CancellationToken;

        var book = await db.Books.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BookId, ct)
            ?? throw new RpcException(new Status(StatusCode.NotFound,
                $"Cartea cu id-ul {request.BookId} nu există."));

        var rating = new Rating
        {
            ReaderId = request.ReaderId,
            BookId = request.BookId,
            Stars = request.Stars,
            CreatedAt = DateTime.UtcNow
        };
        db.Ratings.Add(rating);
        await db.SaveChangesAsync(ct);
        return ToItem(rating, book.Title);
    }

    public override async Task<RatingItem> UpdateRating(UpdateRatingRequest request,
        ServerCallContext context)
    {
        ValidateStars(request.Stars);
        var ct = context.CancellationToken;

        var rating = await db.Ratings.Include(r => r.Book)
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw RatingNotFound(request.Id);

        rating.Stars = request.Stars;
        await db.SaveChangesAsync(ct);
        return ToItem(rating, rating.Book.Title);
    }

    public override async Task<Empty> DeleteRating(RatingId request,
        ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var rating = await db.Ratings.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw RatingNotFound(request.Id);

        db.Ratings.Remove(rating);
        await db.SaveChangesAsync(ct);
        return new Empty();
    }
}