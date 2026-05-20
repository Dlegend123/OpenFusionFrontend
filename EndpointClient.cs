using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace fflauncher
{
    public class EndpointClient : IDisposable
    {
        private static readonly HttpClient SharedHttpClient = CreateSharedClient();
        private readonly HttpClient _httpClient;
        private readonly bool _ownsClient;
        private readonly string _baseHost; // host only or host/path (api.example.com/academy)

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public EndpointClient(string host, HttpClient? httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentNullException(nameof(host));

            _baseHost = NormalizeHost(host);
            if (httpClient is null)
            {
                _httpClient = SharedHttpClient;
                _ownsClient = false;
            }
            else
            {
                _httpClient = httpClient;
                _ownsClient = true;
                if (!_httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("fflauncher/1.0"))
                {
                    // ignore
                }
            }
        }

        private static HttpClient CreateSharedClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            if (!client.DefaultRequestHeaders.UserAgent.TryParseAdd("fflauncher/1.0"))
            {
                // ignore
            }
            return client;
        }

        private static string NormalizeHost(string host)
        {
            ReadOnlySpan<char> span = host.AsSpan().Trim();
            if (span.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                span = span.Slice("https://".Length);
            else if (span.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                span = span.Slice("http://".Length);

            span = span.TrimEnd('/');
            return span.ToString();
        }

        private string MakeUrl(string path, bool offline = false)
        {
            ReadOnlySpan<char> pathSpan = path.AsSpan();
            if (pathSpan.StartsWith('/'))
                pathSpan = pathSpan.Slice(1);
            var httpPrefix = offline ? "http" : "https";

            return pathSpan.IsEmpty
                ? $"{httpPrefix}://{_baseHost}/"
                : $"{httpPrefix}://{_baseHost}/{pathSpan.ToString()}";
        }

        public async Task<InfoResponse> GetInfoAsync()
        {
            var url = MakeUrl("");
            var res = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await HandleResponse<InfoResponse>(res, url).ConfigureAwait(false);
        }

        public async Task<string> GetRefreshTokenAsync(string username, string password)
        {
            var url = MakeUrl("auth");
            var req = new { username, password };
            using var content = CreateJsonContent(req);
            var res = await _httpClient.PostAsync(url, content).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new Exception("Incorrect username or password");
                throw new Exception($"API error {res.StatusCode}: {body} [{url}]");
            }

            return body.Trim();
        }

        public async Task<Session> GetSessionAsync(string refreshToken)
        {
            var url = MakeUrl("auth/session");
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);
            var res = await _httpClient.SendAsync(req).ConfigureAwait(false);
            return await HandleResponse<Session>(res, url).ConfigureAwait(false);
        }

        public async Task<CookieResponse> GetCookieAsync(string token)
        {
            var url = MakeUrl("cookie");
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var res = await _httpClient.SendAsync(req).ConfigureAwait(false);
            return await HandleResponse<CookieResponse>(res, url).ConfigureAwait(false);
        }

        public async Task<string> RegisterUserAsync(string username, string password, string? email = null)
        {
            var url = MakeUrl("account/register");
            var req = new { username, password, email = string.IsNullOrEmpty(email) ? null : email };
            using var content = CreateJsonContent(req);
            var res = await _httpClient.PostAsync(url, content).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                throw new Exception($"API error {res.StatusCode}: {body} [{url}]");
            }
            return body.Trim();
        }

        public async Task<string?> GetCustomIconUrlAsync()
        {
            var url = MakeUrl("launcher/icon.ico");
            using var req = new HttpRequestMessage(HttpMethod.Head, url);
            var res = await _httpClient.SendAsync(req).ConfigureAwait(false);
            if (res.IsSuccessStatusCode)
                return url;
            return null;
        }

        private static async Task<T> HandleResponse<T>(HttpResponseMessage res, string url)
        {
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new Exception($"API error {res.StatusCode}: {body} [{url}]");
            }

            await using var stream = await res.Content.ReadAsStreamAsync().ConfigureAwait(false);
            if (stream == null || stream.CanRead == false)
                return default!;

            try
            {
                var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions).ConfigureAwait(false);
                return result!;
            }
            catch (JsonException ex)
            {
                throw new Exception($"Failed to deserialize response: {ex.Message}.", ex);
            }
        }

        private static ByteArrayContent CreateJsonContent<T>(T value)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return content;
        }

        public void Dispose()
        {
            if (_ownsClient)
            {
                _httpClient.Dispose();
            }
        }

        // Models
        public class InfoResponse
        {
            [JsonPropertyName("server_name")] public string? ServerName { get; set; }
            [JsonPropertyName("api_version")] public string? ApiVersion { get; set; }
            [JsonPropertyName("secure_apis_enabled")] public bool SecureApisEnabled { get; set; }
            [JsonPropertyName("game_version")] public string? GameVersion { get; set; }
            [JsonPropertyName("game_versions")] public string[]? GameVersions { get; set; }
            [JsonPropertyName("login_address")] public string? LoginAddress { get; set; }
            [JsonPropertyName("email_required")] public bool? EmailRequired { get; set; }
            [JsonPropertyName("custom_loading_screen")] public bool? CustomLoadingScreen { get; set; }
        }

        public class Session
        {
            [JsonPropertyName("username")] public string? Username { get; set; }
            [JsonPropertyName("session_token")] public string? SessionToken { get; set; }
        }

        public class CookieResponse
        {
            [JsonPropertyName("username")] public string? Username { get; set; }
            [JsonPropertyName("cookie")] public string? Cookie { get; set; }
            [JsonPropertyName("expires")] public long Expires { get; set; }
        }

        public class RegisterResponse
        {
            [JsonPropertyName("resp")] public string? Resp { get; set; }
            [JsonPropertyName("can_login")] public bool CanLogin { get; set; }
        }

        public class AccountInfo
        {
            [JsonPropertyName("username")] public string? Username { get; set; }
            [JsonPropertyName("email")] public string? Email { get; set; }
        }
    }
}
