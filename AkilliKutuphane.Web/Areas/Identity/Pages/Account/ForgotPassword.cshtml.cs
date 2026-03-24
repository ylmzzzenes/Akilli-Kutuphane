using System.ComponentModel.DataAnnotations;
using AkilliKutuphane.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AkilliKutuphane.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(UserManager<ApplicationUser> userManager, ILogger<ForgotPasswordModel> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "E-posta alanı zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [Display(Name = "E-posta")]
        public string Email { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user is null)
        {
            TempData["ToastSuccess"] = "Eğer bu e-posta ile kayıtlı hesap varsa şifre sıfırlama adımları gönderilecektir.";
            return RedirectToPage("./Login");
        }

        _logger.LogInformation("Şifre sıfırlama talebi alındı. Kullanıcı: {UserId}", user.Id);
        TempData["ToastSuccess"] = "Eğer bu e-posta ile kayıtlı hesap varsa şifre sıfırlama adımları gönderilecektir.";
        return RedirectToPage("./Login");
    }
}
