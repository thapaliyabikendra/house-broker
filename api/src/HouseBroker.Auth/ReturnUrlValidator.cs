namespace HouseBroker.Auth;

/// <summary>Validates redirect targets for SSO pages.</summary>
public static class ReturnUrlValidator
{
    /// <summary>Indicates whether the supplied URL is a safe local redirect target.</summary>
    public static bool IsLocalUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        return returnUrl[0] == '/'
            && (returnUrl.Length == 1
                || (returnUrl[1] != '/' && returnUrl[1] != '\\'));
    }
}
