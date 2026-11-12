using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Lab1_MVC.Data;
using Lab1_MVC.Models;

namespace Lab1_MVC.Controllers
{
    public class OrdersController : Controller
    {
        private readonly LibraryContext _context;

        public OrdersController(LibraryContext context)
        {
            _context = context;
        }

        // GET: Orders?customerId=3
        public async Task<IActionResult> Index(int? customerId)
        {
            var orders = _context.Order.AsQueryable();

            if (customerId != null)
                orders = orders.Where(o => o.CustomerID == customerId);

            var rows = await orders
                .OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderID)
                .Select(o => new OrderListItem(o.OrderID, o.Customer.Name, o.Book.Title, o.OrderDate))
                .ToListAsync();

            ViewData["CustomerID"] = new SelectList(
                _context.Customer.OrderBy(c => c.Name), "CustomerID", "Name", customerId);
            return View(rows);
        }

        // GET: Orders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Order
                .Include(o => o.Customer).Include(o => o.Book)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.OrderID == id);
            if (order == null) return NotFound();

            return View(order);
        }

        // GET: Orders/Create
        public IActionResult Create()
        {
            FillDropdowns(null, null);
            return View(new Order { OrderDate = DateTime.Today });   // data implicită: azi
        }

        // POST: Orders/Create
        // Protecție la overposting: [Bind] cu lista explicită de câmpuri permise.
        // OrderID, Customer și Book nu pot fi setate din formular.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CustomerID,BookID,OrderDate")] Order order)
        {
            // Customer și Book sunt non-nullable (= null!) și MVC le consideră implicit [Required],
            // deși nu vin din formular (doar CustomerID/BookID). Le scoatem din validare.
            ModelState.Remove(nameof(Order.Customer));
            ModelState.Remove(nameof(Order.Book));

            if (ModelState.IsValid)
            {
                _context.Add(order);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            FillDropdowns(order.CustomerID, order.BookID);
            return View(order);
        }

        // GET: Orders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Order.FindAsync(id);
            if (order == null) return NotFound();

            FillDropdowns(order.CustomerID, order.BookID);
            return View(order);
        }

        // POST: Orders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("OrderID,CustomerID,BookID,OrderDate")] Order order)
        {
            // Customer și Book sunt non-nullable (= null!) și MVC le consideră implicit [Required],
            // deși nu vin din formular (doar CustomerID/BookID). Le scoatem din validare.
            ModelState.Remove(nameof(Order.Customer));
            ModelState.Remove(nameof(Order.Book));

            if (id != order.OrderID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(order);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Order.Any(e => e.OrderID == order.OrderID)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            FillDropdowns(order.CustomerID, order.BookID);
            return View(order);
        }

        // GET: Orders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Order
                .Include(o => o.Customer).Include(o => o.Book)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.OrderID == id);
            if (order == null) return NotFound();

            return View(order);
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Order.FindAsync(id);
            if (order != null)
            {
                _context.Order.Remove(order);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private void FillDropdowns(int? customerId, int? bookId)
        {
            ViewData["CustomerID"] = new SelectList(
                _context.Customer.OrderBy(c => c.Name), "CustomerID", "Name", customerId);
            ViewData["BookID"] = new SelectList(
                _context.Book.OrderBy(b => b.Title), "ID", "Title", bookId);
        }
    }
}
