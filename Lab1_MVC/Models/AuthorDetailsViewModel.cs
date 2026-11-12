namespace Lab1_MVC.Models;

public record AuthorBookItem(int ID, string Title, string Genre, decimal Price, int Orders);

public class AuthorDetailsViewModel
{
    public Author Author { get; set; } = null!;
    public List<AuthorBookItem> Books { get; set; } = [];
    public int TotalOrders { get; set; }
    // [V4] varianta B2: anul primei și al ultimei cărți (null dacă autorul nu are cărți)
    public int? FirstYear { get; set; }
    public int? LastYear { get; set; }
}
