using System;
using System.Collections.Generic;
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
        private bool? _secureApisEnabled;

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
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10) // ✅ match Rust
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd("fflauncher/1.0");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

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

        private string MakeUrl(string path)
        {
            ReadOnlySpan<char> pathSpan = path.AsSpan();
            if (pathSpan.StartsWith('/'))
                pathSpan = pathSpan.Slice(1);

            string scheme = (_secureApisEnabled == false) ? "http" : "https";

            return pathSpan.IsEmpty
                ? $"{scheme}://{_baseHost}/"
                : $"{scheme}://{_baseHost}/{pathSpan.ToString()}";
        }

        public async Task<InfoResponse> GetInfoAsync()
        {
            // Try HTTPS first
            var httpsUrl = $"https://{_baseHost}/";

            try
            {
                var res = await _httpClient.GetAsync(httpsUrl).ConfigureAwait(false);
                var info = await HandleResponse<InfoResponse>(res, httpsUrl).ConfigureAwait(false);

                _secureApisEnabled = info?.SecureApisEnabled ?? true;
                return info;
            }
            catch
            {
                // fallback to HTTP
                var httpUrl = $"http://{_baseHost}/";
                var res = await _httpClient.GetAsync(httpUrl).ConfigureAwait(false);
                var info = await HandleResponse<InfoResponse>(res, httpUrl).ConfigureAwait(false);

                _secureApisEnabled = false;
                return info;
            }
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
                if (res.StatusCode == HttpStatusCode.Unauthorized)
                    throw new Exception("Incorrect username or password");

                throw new Exception($"API error {(int)res.StatusCode}: {body} [{url}]");
            }

            if (string.IsNullOrWhiteSpace(body))
                throw new Exception("Empty refresh token response");

            return body.Trim();
        }

        public async Task<Session> GetSessionAsync(string refreshToken)
        {
            var url = MakeUrl("auth/session");
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            SetBearer(req, refreshToken);
            var res = await _httpClient.SendAsync(req).ConfigureAwait(false);
            return await HandleResponse<Session>(res, url).ConfigureAwait(false);
        }

        public async Task<CookieResponse> GetCookieAsync(string token)
        {
            var url = MakeUrl("cookie");
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            SetBearer(req, token);
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

        public async Task<EndpointClient.AccountInfo> GetAccountInfoAsync(string sessionToken)
        {
            var url = MakeUrl("account");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            SetBearer(req, sessionToken);
            var res = await _httpClient.SendAsync(req).ConfigureAwait(false);
            return await HandleResponse<AccountInfo>(res, url).ConfigureAwait(false);
        }

        public async Task<bool> SendOtpAsync(string email)
        {
            var url = MakeUrl("account/otp");
            var req = new { email };
            using var content = CreateJsonContent(req);
            var res = await _httpClient.PostAsync(url, content).ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateEmailAsync(string sessionToken, string newEmail)
        {
            var url = MakeUrl("account/update/email");
            var req = new { new_email = newEmail };
            using var content = CreateJsonContent(req);
            using var httpReq = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            SetBearer(httpReq, sessionToken);
            var res = await _httpClient.SendAsync(httpReq).ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        }

        public async Task<bool> UpdatePasswordAsync(string sessionToken, string newPassword)
        {
            var url = MakeUrl("account/update/password");
            var req = new { new_password = newPassword };
            using var content = CreateJsonContent(req);
            using var httpReq = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            SetBearer(httpReq, sessionToken);
            var res = await _httpClient.SendAsync(httpReq).ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        }

        private static async Task<T> HandleResponse<T>(HttpResponseMessage res, string url)
        {
            var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
            {
                throw new Exception($"API error {(int)res.StatusCode}: {body} [{url}]");
            }

            if (string.IsNullOrWhiteSpace(body))
                return default!;

            try
            {
                return JsonSerializer.Deserialize<T>(body, JsonOptions)!;
            }
            catch (JsonException ex)
            {
                throw new Exception($"JSON parse error: {ex.Message}\nResponse: {body}", ex);
            }
        }
        public async Task<VersionResponse> FetchVersion(Guid versionUuid)
        {
            var uuid = versionUuid.ToString();

            var firstTry = await FetchVersionInternal(uuid);
            if (firstTry != null)
                return ValidateVersion(firstTry, versionUuid);

            // fallback: .json extension
            var secondTry = await FetchVersionInternal($"{uuid}.json");
            if (secondTry != null)
                return ValidateVersion(secondTry, versionUuid);

            throw new Exception($"Failed to fetch version {versionUuid}");
        }

        private async Task<VersionResponse?> FetchVersionInternal(string filename)
        {
            var url = MakeUrl($"versions/{filename}");
            
            try
            {
                var json = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
                return JsonSerializer.Deserialize<VersionResponse>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private VersionResponse ValidateVersion(VersionResponse v, Guid expected)
        {
            if (v.Uuid != expected)
            {
                throw new Exception($"Version mismatch: {v.Uuid} != {expected}");
            }

            return v;
        }

        public class VersionResponse
        {
            [JsonPropertyName("uuid")]
            public Guid Uuid { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("build")]
            public string? Build { get; set; }

            [JsonPropertyName("url")]
            public string? Url { get; set; }

            [JsonPropertyName("parent_uuid")]
            public string? ParentUuid { get; set; }

            [JsonPropertyName("asset_url")]
            public string? AssetUrl { get; set; }

            [JsonPropertyName("main_file_url")]
            public string? MainFileUrl { get; set; }
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
        private static void SetBearer(HttpRequestMessage req, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token cannot be empty");

            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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

            /// <summary>
            /// Returns the list of supported game versions, preferring game_versions array, 
            /// falling back to game_version if necessary.
            /// Matches OpenFusionLauncher behavior.
            /// </summary>
            public List<string> GetSupportedVersions()
            {
                if (GameVersions != null && GameVersions.Length > 0)
                {
                    return new List<string>(GameVersions);
                }
                else if (!string.IsNullOrEmpty(GameVersion))
                {
                    return new List<string> { GameVersion };
                }
                return new List<string>();
            }
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
