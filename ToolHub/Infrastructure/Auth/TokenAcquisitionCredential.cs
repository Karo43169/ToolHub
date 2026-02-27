using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Identity.Web;

namespace ToolHub.Infrastructure.Auth
{
    /// <summary>
    /// TokenCredential, który pobiera access token dla zalogowanego użytkownika
    /// przez Microsoft.Identity.Web (MSAL). Działa z Graph SDK v5.
    /// </summary>
    internal sealed class TokenAcquisitionCredential : TokenCredential
    {
        private readonly ITokenAcquisition _tokenAcquisition;
        private readonly string[] _defaultScopes;

        public TokenAcquisitionCredential(ITokenAcquisition tokenAcquisition, IEnumerable<string> defaultScopes)
        {
            _tokenAcquisition = tokenAcquisition;
            _defaultScopes = defaultScopes?.ToArray() ?? Array.Empty<string>();
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            // TokenRequestContext to struct -> nie jest null
            var scopes = (requestContext.Scopes is { Length: > 0 })
                ? requestContext.Scopes
                : (_defaultScopes.Length > 0 ? _defaultScopes : Array.Empty<string>());

            // Najbardziej kompatybilne przeciążenie – tylko scope’y
            var token = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);

            // Brak jawnej daty wygaśnięcia — ustaw bezpieczny bufor
            var expires = DateTimeOffset.UtcNow.AddMinutes(5);
            return new AccessToken(token, expires);
        }
    }
}