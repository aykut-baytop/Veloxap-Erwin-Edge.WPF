using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Veloxap.AddIn.Erwin.Services
{
    internal sealed class AuthTokenProvider
    {
        private readonly HttpClient httpClient;
        private readonly object syncRoot = new object();

        private string bearerToken;
        private Task<string> loginTask;

        public AuthTokenProvider(HttpClient httpClient)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public Task<string> GetTokenAsync()
        {
            lock (syncRoot)
            {
                if (!string.IsNullOrWhiteSpace(bearerToken))
                    return Task.FromResult(bearerToken);

                if (loginTask == null)
                    loginTask = LoginAsync();

                return loginTask;
            }
        }

        public void ClearToken()
        {
            lock (syncRoot)
            {
                bearerToken = null;
                loginTask = null;
            }
        }

        private async Task<string> LoginAsync()
        {
            try
            {
                string token = await RequestTokenAsync().ConfigureAwait(false);

                lock (syncRoot)
                {
                    bearerToken = token;
                    loginTask = null;
                }

                return token;
            }
            catch
            {
                ClearToken();
                throw;
            }
        }

        private async Task<string> RequestTokenAsync()
        {
            string loginUrl = RuleApiSettings.GetAuthLoginUrl();
            string username = RuleApiSettings.GetAuthUsername();
            string password = RuleApiSettings.GetAuthPassword();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException(
                    "AuthUsername ve AuthPassword bos. Ayarlar ekranindan kullanici adi ve parola bilgilerini doldurun.");

            ApiTraceLogger.Info(
                "LOGIN REQUEST" + Environment.NewLine +
                "Url: " + loginUrl + Environment.NewLine +
                "Username: " + username + Environment.NewLine +
                "Password: ***");

            var serializer = new JavaScriptSerializer();
            string payload = serializer.Serialize(new Dictionary<string, string>
            {
                { "username", username },
                { "password", password }
            });

            using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
            {
                var response = await httpClient.PostAsync(loginUrl, content).ConfigureAwait(false);
                string responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                ApiTraceLogger.Info(
                    "LOGIN RESPONSE" + Environment.NewLine +
                    "Url: " + loginUrl + Environment.NewLine +
                    "Status: " + (int)response.StatusCode + " " + response.ReasonPhrase + Environment.NewLine +
                    "BodyLength: " + (responseJson == null ? 0 : responseJson.Length));

                response.EnsureSuccessStatusCode();

                string token = ExtractToken(responseJson);

                if (string.IsNullOrWhiteSpace(token))
                    throw new InvalidOperationException("Login cevabÄ±nda token bulunamadÄ±.");

                token = NormalizeToken(token);

                ApiTraceLogger.Info(
                    "LOGIN TOKEN" + Environment.NewLine +
                    "TokenFound: true" + Environment.NewLine +
                    "TokenLength: " + token.Length);

                return token;
            }
        }

        private static string ExtractToken(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
                return null;

            var serializer = new JavaScriptSerializer();
            object payload = serializer.DeserializeObject(responseJson);

            var directToken = payload as string;
            if (!string.IsNullOrWhiteSpace(directToken))
                return directToken;

            return ResolveToken(payload);
        }

        private static string ResolveToken(object payload)
        {
            var dictionary = payload as Dictionary<string, object>;
            if (dictionary == null)
                return null;

            object tokenValue = GetDictionaryValue(
                dictionary,
                "token",
                "accessToken",
                "access_token",
                "jwt",
                "jwtToken",
                "bearerToken");

            var token = tokenValue as string;
            if (!string.IsNullOrWhiteSpace(token))
                return token;

            foreach (var value in dictionary.Values)
            {
                token = ResolveToken(value);
                if (!string.IsNullOrWhiteSpace(token))
                    return token;
            }

            return null;
        }

        private static object GetDictionaryValue(Dictionary<string, object> dictionary, params string[] keys)
        {
            if (dictionary == null || keys == null)
                return null;

            foreach (var pair in dictionary)
            {
                if (keys.Any(key => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)))
                    return pair.Value;
            }

            return null;
        }

        private static string NormalizeToken(string token)
        {
            token = token == null ? string.Empty : token.Trim();

            const string bearerPrefix = "Bearer ";
            if (token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
                return token.Substring(bearerPrefix.Length).Trim();

            return token;
        }
    }
}
