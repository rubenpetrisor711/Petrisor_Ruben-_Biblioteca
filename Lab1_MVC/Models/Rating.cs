namespace Lab1_MVC.Models;

// O evaluare (1-5 stele) dată de un cititor unei cărți.
public class Rating
{
    public int ID { get; set; }
    public int ReaderId { get; set; }
    public int BookID { get; set; }
    public Book? Book { get; set; }      // FK către Book: o carte inexistentă dă eroare la salvare
    public int Stars { get; set; }
    public DateTime CreatedAt { get; set; }
}
