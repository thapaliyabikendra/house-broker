using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using HouseBroker.Auth.Resources;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace HouseBroker.Auth.Pages;

public class ConsentModel(IHttpContextAccessor httpContextAccessor, IStringLocalizer<HouseBroker.Auth.Resources.Resources> L) : PageModel
{
    public List<string> Scopes { get; set; } = new();

    public string Title => L["Consent_Title"];
    public string Intro => L["Consent_Intro"];
    public string AllowText => L["Consent_Allow"];
    public string DenyText => L["Consent_Deny"];

    public void OnGet()
    {
        var request = httpContextAccessor.HttpContext?.GetOpenIddictServerRequest();
        if (request != null)
        {
            Scopes = request.GetScopes().ToList();
        }
    }

    public IActionResult OnPostAllow()
    {
        return RedirectToPage("/Index"); // In a real flow, this would signal OpenIddict to grant scopes
    }

    public IActionResult OnPostDeny()
    {
        return RedirectToPage("/Index"); // In a real flow, this would signal OpenIddict to deny
    }
}
