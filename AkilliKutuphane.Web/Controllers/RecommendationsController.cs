using AkilliKutuphane.Business.Services.Interfaces;
using AkilliKutuphane.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AkilliKutuphane.Web.Controllers;

[Authorize]
public class RecommendationsController : Controller
{
    private readonly IRecommendationService _recommendationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public RecommendationsController(
        IRecommendationService recommendationService,
        UserManager<ApplicationUser> userManager)
    {
        _recommendationService = recommendationService;
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

        var recommendations = await _recommendationService.GetPersonalizedRecommendationsAsync(userId, 12, cancellationToken);
        return View(recommendations);
    }
}
