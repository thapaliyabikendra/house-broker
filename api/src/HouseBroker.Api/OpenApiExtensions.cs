using Microsoft.OpenApi;

namespace HouseBroker.Api;

/// <summary>OpenAPI document configuration.</summary>
internal static class OpenApiExtensions
{
    private const string BearerScheme = "Bearer";
    private const string AcceptLanguage = "Accept-Language";

    /// <summary>Registers the API explorer document with shared metadata.</summary>
    internal static IServiceCollection AddHouseBrokerOpenApi(this IServiceCollection services)
        => services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "House Broker API",
                    Version = "v1",
                    Description = "House Broker listing, image, lookup, and commission-rate endpoints. "
                        + "Errors are returned as RFC 7807 ProblemDetails with stable error codes.",
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT access token issued by the House Broker identity provider.",
                };

                document.Components.SecuritySchemes["oauth2"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        AuthorizationCode = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri("http://localhost:5000/sso/connect/authorize"),
                            TokenUrl = new Uri("http://localhost:5000/sso/connect/token"),
                            Scopes = new Dictionary<string, string>
                            {
                                ["openid"] = "OpenID Connect",
                                ["profile"] = "User profile",
                                ["listings.read"] = "Read listings",
                                ["contacts.read"] = "Read broker contact",
                                ["listings.write"] = "Manage listings",
                                ["commission.read"] = "Read commission",
                                ["admin.commission"] = "Manage commission rates",
                            },
                        },
                    },
                    Description = "OAuth 2.0 authorization code flow with PKCE via the House Broker identity provider.",
                };

                document.Security ??= [];
                if (document.Security.Count == 0)
                {
                    document.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [],
                    });
                }

                return Task.CompletedTask;
            });

            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Tags = new HashSet<OpenApiTag>
                {
                    new() { Name = "Listings", Description = "Public browse and broker listing management." },
                    new() { Name = "Images", Description = "Listing image upload, serving, and deletion." },
                    new() { Name = "Lookups", Description = "Property types, features, and deployment metadata." },
                    new() { Name = "Admin: Commission Rates", Description = "Commission rate administration." },
                };

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
                {
                    operation.Security = [];
                }

                operation.Parameters ??= [];
                if (!operation.Parameters.Any(parameter => parameter.Name == AcceptLanguage && parameter.In == ParameterLocation.Header))
                {
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = AcceptLanguage,
                        In = ParameterLocation.Header,
                        Required = false,
                        Description = "Response language: en (default) or ne.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                    });
                }

                return Task.CompletedTask;
            });
        });
}
