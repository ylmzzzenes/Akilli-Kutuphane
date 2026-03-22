using AkilliKutuphane.Business.Services.Interfaces;
using AkilliKutuphane.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AkilliKutuphane.Web.Controllers;

[Authorize]
public class FavoritesController : Controller
{
    private readonly IFavoriteService _favoriteService;
    private readonly UserManager<ApplicationUser> _userManager;

    public FavoritesController(IFavoriteService favoriteService, UserManager<ApplicationUser> userManager)
    {
        _favoriteService = favoriteService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var favorites = await _favoriteService.GetFavoritesAsync(userId, cancellationToken);
        return View(favorites);
    }
}
