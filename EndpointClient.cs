using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace fflauncher
{
    public class EndpointClient : IDisposable
    {
        private readonly HttpClient _httpClient;
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
            _httpClient = httpClient ?? new HttpClient() { Timeout = TimeSpan.FromSeconds(20) };
            if (!_httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("fflauncher/1.0"))
            {
                // ignore
            }
        }

        private static string NormalizeHost(string host)
        {
            host = host.Trim();
            if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                host = host.Substring("https://".Length);
            else if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                host = host.Substring("http://".Length);

            host = host.TrimEnd('/');
            return host;
        }

        private string MakeUrl(string path)
        {
            if (path.StartsWith("/"))
                path = path.Substring(1);
            return string.IsNullOrEmpty(path)
                ? $"https://{_baseHost}/"
                : $"https://{_baseHost}/{path}";
        }

        public async Task<InfoResponse> GetInfoAsync()
        {
            var url = MakeUrl("");
            var res = await _httpClient.GetAsync(url);
            return await HandleResponse<InfoResponse>(res, url);
        }

        public async Task<string> GetRefreshTokenAsync(string username, string password)
        {
            var url = MakeUrl("auth");
            var req = new { username, password };
            var content = new StringContent(JsonSerializer.Serialize(req, JsonOptions), Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync(url, content);
            var body = await res.Content.ReadAsStringAsync();
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
            var res = await _httpClient.SendAsync(req);
            return await HandleResponse<Session>(res, url);
        }

        public async Task<CookieResponse> GetCookieAsync(string token)
        {
            var url = MakeUrl("cookie");
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var res = await _httpClient.SendAsync(req);
            return await HandleResponse<CookieResponse>(res, url);
        }

        public async Task<AccountInfo> GetAccountInfoAsync(string token)
        {
            var url = MakeUrl("account");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var res = await _httpClient.SendAsync(req);
            return await HandleResponse<AccountInfo>(res, url);
        }

        public async Task<RegisterResponse> RegisterUserAsync(string username, string password, string? email = null)
        {
            var url = MakeUrl("account/register");
            var req = new { username, password, email = string.IsNullOrEmpty(email) ? null : email };
            var content = new StringContent(JsonSerializer.Serialize(req, JsonOptions), Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync(url, content);
            return await HandleResponse<RegisterResponse>(res, url);
        }

        public async Task SendOtpAsync(string email)
        {
            var url = MakeUrl("account/otp");
            var content = new StringContent(JsonSerializer.Serialize(new { email }, JsonOptions), Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync(url, content);
            await EnsureSuccess(res, url);
        }

        public async Task UpdateEmailAsync(string token, string newEmail)
        {
            var url = MakeUrl("account/update/email");
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { new_email = newEmail }, JsonOptions), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var res = await _httpClient.SendAsync(req);
            await EnsureSuccess(res, url);
        }

        public async Task UpdatePasswordAsync(string token, string newPassword)
        {
            var url = MakeUrl("account/update/password");
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { new_password = newPassword }, JsonOptions), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var res = await _httpClient.SendAsync(req);
            await EnsureSuccess(res, url);
        }

        public async Task<string?> GetCustomIconUrlAsync()
        {
            var url = MakeUrl("launcher/icon.ico");
            using var req = new HttpRequestMessage(HttpMethod.Head, url);
            var res = await _httpClient.SendAsync(req);
            if (res.IsSuccessStatusCode)
                return url;
            return null;
        }

        private static async Task<T> HandleResponse<T>(HttpResponseMessage res, string url)
        {
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                throw new Exception($"API error {res.StatusCode}: {body} [{url}]");

            if (string.IsNullOrWhiteSpace(body))
                return default!;

            try
            {
                return JsonSerializer.Deserialize<T>(body, JsonOptions)!;
            }
            catch (JsonException ex)
            {
                throw new Exception($"Failed to deserialize response: {ex.Message}. Response content: {body}", ex);
            }
        }

        private static async Task EnsureSuccess(HttpResponseMessage res, string url)
        {
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync();
                throw new Exception($"API error {res.StatusCode}: {body} [{url}]");
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
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
