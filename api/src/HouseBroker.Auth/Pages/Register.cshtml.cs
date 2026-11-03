using Microsoft.AspNetCore.Mvc;
#pragma warning disable CS1591
#pragma warning disable IDE0005
using HouseBroker.Auth.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HouseBroker.Auth.Pages;

public class RegisterModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, RoleManager<IdentityRole<Guid>> roleManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

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

        if (Input.Role != "Seeker" && Input.Role != "Broker")
        {
            ModelState.AddModelError("Input.Role", "Invalid role.");
        }

        if (Input.Password != Input.ConfirmPassword)
        {
            ModelState.AddModelError("Input.ConfirmPassword", "Passwords do not match.");
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

        await signInManager.SignInAsync(user, isPersistent: false);
        if (Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return RedirectToPage("/Index");
    }
}
