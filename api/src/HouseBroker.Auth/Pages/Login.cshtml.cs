using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using HouseBroker.Auth.Resources;
using HouseBroker.Auth.Data;

namespace HouseBroker.Auth.Pages;

public class LoginModel(
    SignInManager<ApplicationUser> signInManager, 
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<HouseBroker.Auth.Resources.Resources> L) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public string Title => L["Login_Title"];
    public string EmailLabel => L["Login_EmailLabel"];
    public string PasswordLabel => L["Login_PasswordLabel"];
    public string RememberMeLabel => L["Login_RememberMe"];
    public string ButtonText => L["Login_Button"];

    public class InputModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.FindByEmailAsync(Input.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, L["Login_InvalidCredentials"]);
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (Url.IsLocalUrl(ReturnUrl))
            {
                return LocalRedirect(ReturnUrl);
            }

            return RedirectToPage("/Index");
        }

        if (result.IsLockedOut)
        {
            return RedirectToPage("./Lockout");
        }

        ModelState.AddModelError(string.Empty, L["Login_InvalidCredentials"]);
        return Page();
    }
}
