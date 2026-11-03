using HouseBroker.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

var pathBase = builder.Configuration["PathBase"] ?? "/api";
app.UsePathBase(pathBase);

app.UseServiceDefaults();

app.Run();