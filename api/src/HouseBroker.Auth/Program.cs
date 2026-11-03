using HouseBroker.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var pathBase = builder.Configuration["PathBase"] ?? "/sso";

var app = builder.Build();

app.UsePathBase(pathBase);

app.UseServiceDefaults();

app.Run();