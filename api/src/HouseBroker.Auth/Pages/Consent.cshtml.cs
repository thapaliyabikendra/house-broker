using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using HouseBroker.Auth.Resources;
using System.Collections.ObjectModel;

namespace HouseBroker.Auth.Pages;

public class ConsentModel(IStringLocalizer<HouseBroker.Auth.Resources.Resources> L) : PageModel
{
    public Collection<string> Scopes { get; } = new();

    public string Title => L["Consent_Title"];
    public string Intro => L["Consent_Intro"];
    public string AllowText => L["Consent_Allow"];
    public string DenyText => L["Consent_Deny"];

    public void OnGet()
    {
        var scopes = HttpContext.Request.Query["scope"].ToString();
        foreach (var scope in scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            Scopes.Add(scope);
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
