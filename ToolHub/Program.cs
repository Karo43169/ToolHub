using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.Graph;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

builder.Services.AddAuthorization(o => o.FallbackPolicy = o.DefaultPolicy);
builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();

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

// Graph SDK v5 przez TokenCredential (Twoje podejœcie jest poprawne)
builder.Services.AddScoped<GraphServiceClient>(sp =>
{
    var tokenAcquisition = sp.GetRequiredService<ITokenAcquisition>();
    var credential = new ToolHub.Infrastructure.Auth.TokenAcquisitionCredential(
        tokenAcquisition, new[] { "User.Read" });
    return new GraphServiceClient(credential, new[] { "User.Read" });
});

// Rejestracje Twoich serwisów domenowych
builder.Services.AddScoped<ToolHub.Application.Abstractions.IToolStore, ToolHub.Infrastructure.None.NoneToolStore>();

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