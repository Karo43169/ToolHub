using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.Graph;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Global impersonation toggle (1 = enabled, 0 = disabled)
// Edit this value to enable/disable query-string impersonation during local testing.
// ?impersonateEmail=jan.kowalski@firma.pl
ToolHub.Infrastructure.Impersonation.ImpersonationSettings.Enabled = 0;

// Wczytaj zmienne środowiskowe
builder.Configuration.AddEnvironmentVariables();

// Mapowanie ENV ? AzureAd 
builder.Configuration["AzureAd:ClientId"] = Environment.GetEnvironmentVariable("CLIENT_ID");
builder.Configuration["AzureAd:TenantId"] = Environment.GetEnvironmentVariable("TENANT_ID");
builder.Configuration["AzureAd:ClientSecret"] = Environment.GetEnvironmentVariable("CLIENT_SECRET");

//graph app-only

builder.Services.AddSingleton<GraphServiceClient>(sp =>
{
    var tenantId = builder.Configuration["AzureAd:TenantId"];
    var clientId = builder.Configuration["AzureAd:ClientId"];
    var clientSecret = builder.Configuration["AzureAd:ClientSecret"];

    var credential = new ClientSecretCredential(
        tenantId,
        clientId,
        clientSecret
    );

    return new GraphServiceClient(credential);
});


builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

builder.Services.AddAuthorization(o => o.FallbackPolicy = o.DefaultPolicy);
builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();
builder.Services.AddScoped<ToolHub.Infrastructure.SharePoint.SharePointToolRequestService>();
builder.Services.AddScoped<ToolHub.Infrastructure.SharePoint.SharePointAdminService>();
builder.Services.AddScoped<ToolHub.Infrastructure.SharePoint.SharePointToolRequestReader>();
builder.Services.AddScoped<ToolHub.State.AdminRequestState>();
builder.Services.AddScoped<ToolHub.Infrastructure.SharePoint.SharePointToolApprovalService>();
builder.Services.AddScoped<ToolHub.Infrastructure.SharePoint.SharePointToolPublishService>();
builder.Services.AddScoped<ToolHub.Infrastructure.SharePoint.SharePointToolLocationService>();
builder.Services.AddScoped<ToolHub.Infrastructure.SharePoint.SharePointAdminLockService>();
builder.Services.AddScoped<ToolHub.Infrastructure.SharePoint.SharePointRejectedArchiveService>();
builder.Services.AddScoped<ToolHub.State.AdminRequestViewState>();
builder.Services.AddScoped<ToolHub.Infrastructure.SharePoint.SharePointToolHistoryCleanupService>();
builder.Services.AddScoped<ToolHub.Infrastructure.SharePoint.SharePointToolDeleteService>();
builder.Services.AddScoped<ToolHub.Infrastructure.SharePoint.SharePointToolUpdateService>();
builder.Services.AddSingleton<ToolHub.Infrastructure.SharePoint.ToolCatalogCache>();
builder.Services.AddHostedService<ToolHub.Infrastructure.SharePoint.ToolCatalogWarmupService>();
builder.Services.AddScoped<ToolHub.Infrastructure.SharePoint.SharePointToolFavoriteService>();

// Zakresy OIDC + Graph
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

// Graph SDK v5 przez TokenCredential
//builder.Services.AddScoped<GraphServiceClient>(sp =>
//{
   // var tokenAcquisition = sp.GetRequiredService<ITokenAcquisition>();
  //  var credential = new ToolHub.Infrastructure.Auth.TokenAcquisitionCredential(
 //       tokenAcquisition, new[] { "User.Read" });
 //   return new GraphServiceClient(credential, new[] { "User.Read" });
//});

// Rejestracje serwisów domenowych
builder.Services.AddScoped<ToolHub.Application.Abstractions.IToolStore, ToolHub.Infrastructure.SharePoint.SharePointToolStore>();

// Stany (state) aplikacji
builder.Services.AddScoped<ToolHub.State.ThemeState>();
builder.Services.AddScoped<ToolHub.State.ToolHubState>();
builder.Services.AddScoped<ToolHub.State.CategoryState>();

var app = builder.Build();

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
app.MapRazorComponents<ToolHub.Components.App>().AddInteractiveServerRenderMode();

app.Run();