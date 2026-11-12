using Biblioteca.Portal.Models;
using BibliotecaGrpc;
using Grpc.Core;
using Lab1_MVC.Models;
using Microsoft.AspNetCore.Mvc;

namespace Biblioteca.Portal.Controllers;

public class RatingsController(ReaderRatings.ReaderRatingsClient client) : Controller
{
    private static DateTime Deadline() => DateTime.UtcNow.AddSeconds(5);

    private static string Msg(RpcException ex) => $"{ex.StatusCode}: {ex.Status.Detail}";

    [HttpGet]
    public async Task<IActionResult> Index(int readerId = 1000, CancellationToken ct = default)
    {
        var vm = new RatingsViewModel { ReaderId = readerId };
        if (TempData["Error"] is string err) vm.Error = err;
        try
        {
            using var call = client.ListRatings(
                new ReaderRequest { ReaderId = readerId }, deadline: Deadline());
            await foreach (var item in call.ResponseStream.ReadAllAsync(ct))
                vm.Ratings.Add(item);
        }
        catch (RpcException ex) { vm.Error = Msg(ex); }
        return View(vm);
    }

    [HttpGet]
    public IActionResult Create(int readerId = 1000) =>
        View(new NewRating { ReaderId = readerId, Stars = 3 });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NewRating model)
    {
        try
        {
            await client.AddRatingAsync(model, deadline: Deadline());
            return RedirectToAction(nameof(Index), new { readerId = model.ReaderId });
        }
        catch (RpcException ex)
        {
            ViewBag.Error = Msg(ex);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var item = await client.GetRatingAsync(new RatingId { Id = id }, deadline: Deadline());
            return View(item);
        }
        catch (RpcException ex)
        {
            TempData["Error"] = Msg(ex);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, int stars, int readerId = 1000)
    {
        try
        {
            await client.UpdateRatingAsync(
                new UpdateRatingRequest { Id = id, Stars = stars }, deadline: Deadline());
            return RedirectToAction(nameof(Index), new { readerId });
        }
        catch (RpcException ex)
        {
            ViewBag.Error = Msg(ex);
            return View(new RatingItem { Id = id, Stars = stars });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var item = await client.GetRatingAsync(new RatingId { Id = id }, deadline: Deadline());
            return View(item);
        }
        catch (RpcException ex)
        {
            TempData["Error"] = Msg(ex);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, int readerId = 1000)
    {
        try
        {
            await client.DeleteRatingAsync(new RatingId { Id = id }, deadline: Deadline());
        }
        catch (RpcException ex)
        {
            TempData["Error"] = Msg(ex);
        }
        return RedirectToAction(nameof(Index), new { readerId });
    }
}