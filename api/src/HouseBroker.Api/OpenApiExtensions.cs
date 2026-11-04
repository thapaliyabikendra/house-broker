using HouseBroker.Api.Filters;
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

                var method = context.Description.HttpMethod;
                var path = context.Description.RelativePath ?? string.Empty;

                var isIdempotent = context.Description.ActionDescriptor.EndpointMetadata.OfType<IdempotentAttribute>().Any();
                if (isIdempotent)
                {
                    if (!operation.Parameters.Any(parameter => parameter.Name == "Idempotency-Key" && parameter.In == ParameterLocation.Header))
                    {
                        operation.Parameters.Add(new OpenApiParameter
                        {
                            Name = "Idempotency-Key",
                            In = ParameterLocation.Header,
                            Required = false,
                            Description = "Optional idempotency key (max 128 chars). Replay returns the original response with Idempotent-Replayed: true.",
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 128 },
                        });
                    }

                    operation.Responses ??= new OpenApiResponses();
                    const string unprocessable = "422";
                    if (!operation.Responses.ContainsKey(unprocessable))
                    {
                        operation.Responses[unprocessable] = new OpenApiResponse
                        {
                            Description = "Unprocessable entity: invalid or too-long idempotency key.",
                            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["application/problem+json"] = new OpenApiMediaType
                                {
                                    Schema = new OpenApiSchemaReference("ProblemDetails"),
                                },
                            },
                        };
                    }
                }

                if (string.Equals(method, HttpMethods.Put, StringComparison.OrdinalIgnoreCase))
                {
                    if (!operation.Parameters.Any(parameter => parameter.Name == "If-Match" && parameter.In == ParameterLocation.Header))
                    {
                        operation.Parameters.Add(new OpenApiParameter
                        {
                            Name = "If-Match",
                            In = ParameterLocation.Header,
                            Required = true,
                            Description = "Required strong ETag (rowversion) for concurrency control. Returns 428 if missing and 412 if stale.",
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                        });
                    }
                }

                if (string.Equals(method, HttpMethods.Put, StringComparison.OrdinalIgnoreCase))
                {
                    operation.Responses ??= new OpenApiResponses();
                    if (!operation.Responses.ContainsKey("412"))
                    {
                        operation.Responses["412"] = new OpenApiResponse { Description = "Precondition failed: the If-Match ETag is stale." };
                    }

                    if (!operation.Responses.ContainsKey("428"))
                    {
                        operation.Responses["428"] = new OpenApiResponse { Description = "Precondition required: the If-Match header is missing." };
                    }
                }

                if (string.Equals(method, HttpMethods.Get, StringComparison.OrdinalIgnoreCase)
                    && path.Contains("{id}", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var (_, response) in operation.Responses ?? new OpenApiResponses())
                    {
                        if (response is null)
                        {
                            continue;
                        }

                        if (response is Microsoft.OpenApi.OpenApiResponse concreteResponse)
                        {
                            concreteResponse.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.OrdinalIgnoreCase);
                            if (!concreteResponse.Headers.ContainsKey("ETag"))
                            {
                                concreteResponse.Headers["ETag"] = new OpenApiHeader
                                {
                                    Description = "Strong ETag based on the listing row version.",
                                    Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                                };
                            }
                        }
                    }
                }

                return Task.CompletedTask;
            });
        });
}
