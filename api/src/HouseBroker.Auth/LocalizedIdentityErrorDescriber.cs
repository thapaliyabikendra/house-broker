using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using HouseBroker.Auth.Resources;

namespace HouseBroker.Auth;

/// <summary>Localized identity error describer.</summary>
public sealed class LocalizedIdentityErrorDescriber(IStringLocalizer<HouseBroker.Auth.Resources.Resources> localizer) : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => new() { Code = nameof(DefaultError), Description = localizer["Identity_DefaultError"] };
    public override IdentityError ConcurrencyFailure() => new() { Code = nameof(ConcurrencyFailure), Description = localizer["Identity_ConcurrencyFailure"] };
    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = localizer["Identity_PasswordMismatch"] };
    public override IdentityError InvalidToken() => new() { Code = nameof(InvalidToken), Description = localizer["Identity_InvalidToken"] };
    public override IdentityError LoginAlreadyAssociated() => new() { Code = nameof(LoginAlreadyAssociated), Description = localizer["Identity_LoginAlreadyAssociated"] };
    public override IdentityError InvalidUserName(string? userName) => new() { Code = nameof(InvalidUserName), Description = localizer["Identity_InvalidUserName", userName ?? string.Empty] };
    public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = localizer["Identity_InvalidEmail", email ?? string.Empty] };
    public override IdentityError DuplicateUserName(string? userName) => new() { Code = nameof(DuplicateUserName), Description = localizer["Identity_DuplicateUserName", userName ?? string.Empty] };
    public override IdentityError DuplicateEmail(string? email) => new() { Code = nameof(DuplicateEmail), Description = localizer["Identity_DuplicateEmail", email ?? string.Empty] };
    public override IdentityError UserAlreadyHasPassword() => new() { Code = nameof(UserAlreadyHasPassword), Description = localizer["Identity_UserAlreadyHasPassword"] };
    public override IdentityError UserLockoutNotEnabled() => new() { Code = nameof(UserLockoutNotEnabled), Description = localizer["Identity_UserLockoutNotEnabled"] };
    public override IdentityError UserAlreadyInRole(string? role) => new() { Code = nameof(UserAlreadyInRole), Description = localizer["Identity_UserAlreadyInRole", role ?? string.Empty] };
    public override IdentityError UserNotInRole(string? role) => new() { Code = nameof(UserNotInRole), Description = localizer["Identity_UserNotInRole", role ?? string.Empty] };
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = localizer["Identity_PasswordTooShort", length] };
    public override IdentityError PasswordRequiresNonAlphanumeric() => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = localizer["Identity_PasswordRequiresNonAlphanumeric"] };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = localizer["Identity_PasswordRequiresDigit"] };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = localizer["Identity_PasswordRequiresLower"] };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = localizer["Identity_PasswordRequiresUpper"] };
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new() { Code = nameof(PasswordRequiresUniqueChars), Description = localizer["Identity_PasswordRequiresUniqueChars", uniqueChars] };
    public override IdentityError RecoveryCodeRedemptionFailed() => new() { Code = nameof(RecoveryCodeRedemptionFailed), Description = localizer["Identity_RecoveryCodeRedemptionFailed"] };
}
