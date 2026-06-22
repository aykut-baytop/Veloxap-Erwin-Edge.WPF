using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Veloxap.AddIn.Erwin.Services
{
    internal sealed class BearerTokenHandler : DelegatingHandler
    {
        private readonly AuthTokenProvider tokenProvider;

        public BearerTokenHandler(AuthTokenProvider tokenProvider, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            this.tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string token = await tokenProvider.GetTokenAsync().ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
