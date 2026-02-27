using ToolHub.Components;
using ToolHub.Application.Abstractions;
using ToolHub.Infrastructure.None;

using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// === Twoje serwisy ===
builder.Services.AddScoped<IToolStore, NoneToolStore>();
builder.Services.AddScoped<ToolHub.State.ThemeState>();
builder.Services.AddScoped<ToolHub.State.ToolHubState>();
builder.Services.AddScoped<ToolHub.State.CategoryState>();

// === [NOWE] Uwierzytelnianie i autoryzacja z Entra ID ===

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy; // WYMUSZA logowanie globalnie
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseStaticFiles(); // (wa¿ne: statyki przed antiforgery)

app.UseAuthentication(); // [NOWE] — musi byæ przed UseAuthorization
app.UseAuthorization();  // [NOWE]

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();