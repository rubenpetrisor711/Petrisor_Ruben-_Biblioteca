using BibliotecaGrpc;
using Grpc.Core;
using Grpc.Net.Client;

// Portul https al serverului (din launchSettings.json: https://localhost:7105)
using var channel = GrpcChannel.ForAddress("https://localhost:7105");
var client = new Recommender.RecommenderClient(channel);

while (true)
{
    Console.WriteLine();
    Console.WriteLine("1 · Unary   2 · Server streaming   3 · Client streaming   0 · Iesire");
    Console.Write("Alegere: ");
    var choice = Console.ReadLine();
    if (choice is null or "0") break;   // null = nu mai există intrare (Ctrl+D)

    Console.Write("ReaderId: ");
    int readerId = int.TryParse(Console.ReadLine(), out var r) ? r : 1;

    try
    {
        switch (choice)
        {
            case "1": // un singur apel, un singur răspuns cu toată lista
            {
                var reply = await client.RecommendAsync(new RecommendRequest { ReaderId = readerId, Top = 5 });
                foreach (var b in reply.Books)
                    Console.WriteLine($"  {b.BookId,3}  {b.Title,-40} {b.Score:F2}  fallback={b.Fallback}");
                break;
            }
            case "2": // serverul trimite câte o carte, pe rând
            {
                var started = DateTime.Now;
                using var call = client.StreamRecommendations(new RecommendRequest { ReaderId = readerId, Top = 5 });
                await foreach (var b in call.ResponseStream.ReadAllAsync())
                    Console.WriteLine($"  [+{(DateTime.Now - started).TotalMilliseconds,5:F0} ms] {b.BookId,3}  {b.Title,-40} {b.Score:F2}  fallback={b.Fallback}");
                break;
            }
            case "3": // clientul trimite mai multe evaluări, serverul răspunde o dată cu un rezumat
            {
                Console.WriteLine("Introdu evaluări 'bookId stele' (linie goală = terminat). Ex: 12 5");
                using var call = client.RateBooks();
                while (true)
                {
                    var line = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) break;
                    var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    await call.RequestStream.WriteAsync(new RatingInput
                    {
                        ReaderId = readerId,
                        BookId = int.Parse(p[0]),
                        Stars = int.Parse(p[1])
                    });
                }
                await call.RequestStream.CompleteAsync();   // "am terminat de trimis"
                var summary = await call;                    // așteptăm răspunsul final
                Console.WriteLine($"  Salvate: {summary.Saved}, respinse: {summary.Rejected}, medie: {summary.Average:F2}");
                break;
            }
        }
    }
    catch (RpcException ex)
    {
        Console.WriteLine($"  EROARE gRPC: {ex.StatusCode} — {ex.Status.Detail}");
    }
}
