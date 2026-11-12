using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lab1_MVC.Data;
using Lab1_MVC.Models;

namespace Lab1_MVC.Controllers
{
    public class CustomersController : Controller
    {
        private readonly LibraryContext _context;

        public CustomersController(LibraryContext context)
        {
            _context = context;
        }

        // GET: Customers
        public async Task<IActionResult> Index()
        {
            return View(await _context.Customer.OrderBy(x => x.Name).ToListAsync());
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Customer.FirstOrDefaultAsync(m => m.CustomerID == id);
            if (item == null) return NotFound();

            return View(item);
        }

        // GET: Customers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Adress,BirthDate")] Customer item)
        {
            if (ModelState.IsValid)
            {
                _context.Add(item);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(item);
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Customer.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CustomerID,Name,Adress,BirthDate")] Customer item)
        {
            if (id != item.CustomerID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(item);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Customer.Any(e => e.CustomerID == item.CustomerID)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(item);
        }

        // GET: Customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Customer.FirstOrDefaultAsync(m => m.CustomerID == id);
            if (item == null) return NotFound();

            return View(item);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Customer.FindAsync(id);
            if (item != null)
            {
                _context.Customer.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
