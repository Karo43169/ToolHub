using System;
using Azure.Core;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

using ToolHub.Components;
using ToolHub.Application.Abstractions;
using ToolHub.Infrastructure.None;
using ToolHub.Infrastructure.Graph;   

var builder = WebApplication.CreateBuilder(args);

// =====================
// Razor Components
// =====================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

// =====================
// Moje serwisy
// =====================
builder.Services.AddScoped<IToolStore, NoneToolStore>();
builder.Services.AddScoped<ToolHub.State.ThemeState>();
builder.Services.AddScoped<ToolHub.State.ToolHubState>();
builder.Services.AddScoped<ToolHub.State.CategoryState>();

// =====================
// Uwierzytelnianie i autoryzacja
// =====================
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

// ¯¹damy potrzebnych scope’ów
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("offline_access");
    options.Scope.Add("User.Read");
});

// =====================
// Microsoft Graph v5 – rejestracja przez extension method
// =====================
builder.Services.AddGraphWithMsal("User.Read");

var app = builder.Build();

// =====================
// Pipeline HTTP
// =====================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();