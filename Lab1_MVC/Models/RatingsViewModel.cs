using BibliotecaGrpc;

namespace Biblioteca.Portal.Models;

public class RatingsViewModel
{
    public int ReaderId { get; set; } = 1000;
    public List<RatingItem> Ratings { get; set; } = [];
    public string? Error { get; set; }
}