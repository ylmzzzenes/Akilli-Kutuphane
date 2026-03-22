using AkilliKutuphane.Business.Services.Interfaces;
using AkilliKutuphane.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AkilliKutuphane.Web.Controllers;

[Authorize]
public class RatingsController : Controller
{
    private readonly IRatingService _ratingService;
    private readonly UserManager<ApplicationUser> _userManager;

    public RatingsController(IRatingService ratingService, UserManager<ApplicationUser> userManager)
    {
        _ratingService = ratingService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var ratedBooks = await _ratingService.GetRatedBooksAsync(userId, cancellationToken);
        return View(ratedBooks);
    }
}
