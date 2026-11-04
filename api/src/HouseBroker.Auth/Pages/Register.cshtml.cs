using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using HouseBroker.Auth.Resources;
using HouseBroker.Auth.Data;

namespace HouseBroker.Auth.Pages;

public class RegisterModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IStringLocalizer<HouseBroker.Auth.Resources.Resources> L) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public string Title => L["Register_Title"];
    public string EmailLabel => L["Register_EmailLabel"];
    public string PasswordLabel => L["Register_PasswordLabel"];
    public string ConfirmPasswordLabel => L["Register_ConfirmPasswordLabel"];
    public string RoleLabel => L["Register_RoleLabel"];
    public string FullNameLabel => L["Register_FullNameLabel"];
    public string PhoneLabel => L["Register_PhoneLabel"];
    public string ButtonText => L["Register_Button"];

    public class InputModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string Role { get; set; } = "Seeker";
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");

        if (!AuthClaimMapper.RegistrationRoles.Contains(Input.Role, StringComparer.Ordinal))
        {
            ModelState.AddModelError("Input.Role", L["Register_InvalidRole"]);
        }

        if (Input.Role == "Broker")
        {
            if (string.IsNullOrWhiteSpace(Input.FullName))
            {
                ModelState.AddModelError("Input.FullName", L["Register_BrokerFullNameRequired"]);
            }

            if (string.IsNullOrWhiteSpace(Input.PhoneNumber))
            {
                ModelState.AddModelError("Input.PhoneNumber", L["Register_BrokerPhoneRequired"]);
            }
        }

        if (Input.Password != Input.ConfirmPassword)
        {
            ModelState.AddModelError("Input.ConfirmPassword", L["Register_PasswordMismatch"]);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!await roleManager.RoleExistsAsync(Input.Role))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(Input.Role));
        }

        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            FullName = Input.FullName,
            PhoneNumber = string.IsNullOrWhiteSpace(Input.PhoneNumber) ? null : Input.PhoneNumber,
        };

        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        await userManager.AddToRoleAsync(user, Input.Role);

        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            await userManager.AddClaimAsync(user, new Claim("name", user.FullName));
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            await userManager.AddClaimAsync(user, new Claim("email", user.Email));
        }

        if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            await userManager.AddClaimAsync(user, new Claim("phone_number", user.PhoneNumber));
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        if (Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return RedirectToPage("/Index");
    }
}
