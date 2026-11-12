using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lab1_MVC.Data;
using Lab1_MVC.Models;

namespace Lab1_MVC.Controllers
{
    public class AuthorsController : Controller
    {
        private readonly LibraryContext _context;

        public AuthorsController(LibraryContext context)
        {
            _context = context;
        }

        // GET: Authors
        public async Task<IActionResult> Index()
        {
            return View(await _context.Author.OrderBy(x => x.LastName).ToListAsync());
        }

        // GET: Authors/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var author = await _context.Author.AsNoTracking().FirstOrDefaultAsync(m => m.ID == id);
            if (author == null) return NotFound();

            // Cărțile autorului, cu proiecție: titlu, gen, preț, număr de comenzi
            var books = await _context.Book
                .Where(b => b.AuthorID == id)
                .OrderBy(b => b.Title)
                .Select(b => new AuthorBookItem(b.ID, b.Title,
                    b.Genre != null ? b.Genre.Name : "", b.Price, b.Orders.Count))
                .ToListAsync();

            // Numărul total de comenzi pentru toate cărțile autorului (COUNT în SQL)
            var totalOrders = await _context.Order.CountAsync(o => o.Book.AuthorID == id);

            // [V4] anul primei și al ultimei cărți: MIN/MAX în SQL
            var years = await _context.Book
                .Where(b => b.AuthorID == id)
                .GroupBy(b => b.AuthorID)
                .Select(g => new { First = g.Min(b => b.Year), Last = g.Max(b => b.Year) })
                .FirstOrDefaultAsync();

            return View(new AuthorDetailsViewModel
            {
                Author = author,
                Books = books,
                TotalOrders = totalOrders,
                FirstYear = years?.First,
                LastYear = years?.Last
            });
        }

        // GET: Authors/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Authors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FirstName,LastName")] Author item)
        {
            if (ModelState.IsValid)
            {
                _context.Add(item);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(item);
        }

        // GET: Authors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Author.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        // POST: Authors/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,FirstName,LastName")] Author item)
        {
            if (id != item.ID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(item);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Author.Any(e => e.ID == item.ID)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(item);
        }

        // GET: Authors/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Author.FirstOrDefaultAsync(m => m.ID == id);
            if (item == null) return NotFound();

            return View(item);
        }

        // POST: Authors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Author.FindAsync(id);
            if (item != null)
            {
                _context.Author.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
