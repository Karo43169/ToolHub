using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using ToolHub.Infrastructure.Auth;

namespace ToolHub.Infrastructure.Graph
{
    /// <summary>
    /// Rejestracja Microsoft Graph SDK v5 z obsługą MSAL (ITokenAcquisition).
    /// </summary>
    public static class GraphServiceCollectionExtensions
    {
        public static IServiceCollection AddGraphWithMsal(this IServiceCollection services, params string[] userScopes)
        {
            if (userScopes is null || userScopes.Length == 0)
                userScopes = new[] { "User.Read" };

            services.AddScoped<GraphServiceClient>(sp =>
            {
                var tokenAcquisition = sp.GetRequiredService<ITokenAcquisition>();

                var credential = new TokenAcquisitionCredential(tokenAcquisition, userScopes);

                // W SDK v5 można przekazać scope’y delegowane
                return new GraphServiceClient(credential, userScopes);
            });

            return services;
        }
    }
}