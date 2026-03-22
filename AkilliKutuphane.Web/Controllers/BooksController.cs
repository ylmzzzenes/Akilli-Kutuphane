using AkilliKutuphane.Business.Services.Interfaces;
using AkilliKutuphane.Data.Entities;
using AkilliKutuphane.Web.Models.Books;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AkilliKutuphane.Web.Controllers;

public class BooksController : Controller
{
    private const int DefaultPageSize = 12;
    private readonly IBookService _bookService;
    private readonly IFavoriteService _favoriteService;
    private readonly IRatingService _ratingService;
    private readonly IRecommendationService _recommendationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BooksController> _logger;

    public BooksController(
        IBookService bookService,
        IFavoriteService favoriteService,
        IRatingService ratingService,
        IRecommendationService recommendationService,
        UserManager<ApplicationUser> userManager,
        ILogger<BooksController> logger)
    {
        _bookService = bookService;
        _favoriteService = favoriteService;
        _ratingService = ratingService;
        _recommendationService = recommendationService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? query, string? author, int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User);
        var result = await _bookService.SearchBooksAsync(query, author, page, DefaultPageSize, userId, cancellationToken);

        return View(new BookListPageViewModel
        {
            Query = query,
            Author = author,
            Result = result
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id, string? returnUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction(nameof(Index));
        }

        var userId = _userManager.GetUserId(User);
        var book = await _bookService.GetBookDetailAsync(id, userId, cancellationToken);
        if (book is null)
        {
            TempData["ToastError"] = "Kitap detayı alınamadı.";
            return RedirectToAction(nameof(Index));
        }

        return View(new BookDetailPageViewModel
        {
            Book = book,
            ReturnUrl = returnUrl
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(string externalId, bool isFavorite, string? returnUrl, CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(externalId))
        {
            TempData["ToastError"] = "Favori işlemi için giriş yapmalısınız.";
            return RedirectToLocal(returnUrl);
        }

        try
        {
            if (isFavorite)
            {
                await _favoriteService.RemoveFavoriteAsync(userId, externalId, cancellationToken);
                TempData["ToastSuccess"] = "Kitap favorilerden çıkarıldı.";
            }
            else
            {
                await _favoriteService.AddFavoriteAsync(userId, externalId, cancellationToken);
                TempData["ToastSuccess"] = "Kitap favorilere eklendi.";
            }

            await _recommendationService.InvalidateUserRecommendationsAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Favorite toggle failed for user {UserId} and book {ExternalId}", userId, externalId);
            TempData["ToastError"] = "Favori işlemi sırasında bir hata oluştu.";
        }

        return RedirectToLocal(returnUrl);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rate(RateBookInputModel input, CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            TempData["ToastError"] = "Puan vermek için giriş yapmalısınız.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _ratingService.UpsertRatingAsync(userId, input.ExternalId, input.Score, cancellationToken);
            await _recommendationService.InvalidateUserRecommendationsAsync(userId);
            TempData["ToastSuccess"] = "Puanınız kaydedildi.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rating failed for user {UserId} and book {ExternalId}", userId, input.ExternalId);
            TempData["ToastError"] = "Puan kaydedilirken hata oluştu.";
        }

        return RedirectToLocal(input.ReturnUrl);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}
