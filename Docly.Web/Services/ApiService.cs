using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Docly.Web.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;
        private const string TokenKey = "docly.token";

        public ApiService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }

        private async Task<string?> GetTokenAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TokenKey);
            }
            catch
            {
                return null;
            }
        }

        private async Task ClearTokenAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
            }
            catch
            {
                // ignore
            }
        }

        private async Task AttachAuthAsync(HttpRequestMessage request)
        {
            var token = await GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        private async Task<T?> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            try
            {
                await AttachAuthAsync(request);
                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await ClearTokenAsync();
                    throw new UnauthorizedAccessException("Your session has expired. Please sign in again.");
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(await ReadError(response));
                }
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    return default;
                }

                var stream = await response.Content.ReadAsStreamAsync();
                if (stream.Length == 0)
                {
                    return default;
                }
                return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"API request failed: {ex.Message}", ex);
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private async Task<T?> GetAsync<T>(string endpoint)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            return await SendAsync<T>(request);
        }

        private async Task<T?> PostAsync<T>(string endpoint, object? body = null, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            if (body is HttpContent httpContent)
            {
                request.Content = httpContent;
            }
            else if (body != null)
            {
                request.Content = JsonContent.Create(body, options: JsonOptions);
            }
            return await SendAsync<T>(request, cancellationToken);
        }

        private async Task<T?> PatchAsync<T>(string endpoint, object? body = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint);
            if (body != null)
            {
                request.Content = JsonContent.Create(body, options: JsonOptions);
            }
            return await SendAsync<T>(request);
        }

        private async Task<bool> DeleteAsync(string endpoint)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            try
            {
                await AttachAuthAsync(request);
                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await ClearTokenAsync();
                    throw new UnauthorizedAccessException("Your session has expired. Please sign in again.");
                }
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException(await ReadError(response));
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"API request failed: {ex.Message}", ex);
            }
        }

        private async Task<string> ReadError(HttpResponseMessage response)
        {
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            // Matches application/json and application/problem+json (Results.Problem responses).
            if (!contentType.EndsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                var text = await response.Content.ReadAsStringAsync();
                return string.IsNullOrWhiteSpace(text) ? "Something went wrong. Please try again." : text;
            }
            try
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (body.ValueKind == JsonValueKind.Object)
                {
                    if (body.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                        return detail.GetString() ?? "Something went wrong. Please try again.";
                    if (body.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                        return title.GetString() ?? "Something went wrong. Please try again.";
                    if (body.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                        return message.GetString() ?? "Something went wrong. Please try again.";
                }
                if (body.ValueKind == JsonValueKind.String)
                {
                    return body.GetString() ?? "Something went wrong. Please try again.";
                }
                return "Something went wrong. Please try again.";
            }
            catch
            {
                return "Something went wrong. Please try again.";
            }
        }

        // Auth endpoints
        public async Task<AuthResponse?> LoginAsync(string email, string password)
        {
            return await PostAsync<AuthResponse>("/api/auth/login", new { email, password });
        }

        public async Task<AuthResponse?> RegisterAsync(string email, string password)
        {
            return await PostAsync<AuthResponse>("/api/auth/register", new { email, password });
        }

        // Document endpoints
        public async Task<List<DocumentDto>?> GetDocumentsAsync()
        {
            return await GetAsync<List<DocumentDto>>("/api/documents/");
        }

        public async Task<DocumentDto?> UploadDocumentAsync(string title, Stream fileStream, string fileName, string? contentType)
        {
            var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(fileStream);
            // The API validates the part's Content-Type; without it every upload is rejected.
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(ResolveContentType(fileName, contentType));
            content.Add(fileContent, "file", fileName);
            content.Add(new StringContent(title), "title");
            return await PostAsync<DocumentDto>("/api/documents/", content);
        }

        private static string ResolveContentType(string fileName, string? browserType)
        {
            if (!string.IsNullOrWhiteSpace(browserType))
                return browserType;

            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }

        public async Task<bool> DeleteDocumentAsync(Guid documentId)
        {
            return await DeleteAsync($"/api/documents/{documentId}");
        }

        public async Task<DocumentDto?> RetryDocumentAsync(Guid documentId)
        {
            return await PostAsync<DocumentDto>($"/api/documents/{documentId}/retry");
        }

        // Chat endpoints
        public async Task<ChatResponse?> AskQuestionAsync(string question, Guid? conversationId = null, CancellationToken cancellationToken = default)
        {
            return await PostAsync<ChatResponse>("/api/chat/", new { question, conversationId }, cancellationToken);
        }

        // Chat history endpoints
        public async Task<List<ChatConversationDto>?> GetConversationsAsync()
        {
            return await GetAsync<List<ChatConversationDto>>("/api/conversations/");
        }

        public async Task<ChatConversationDto?> CreateConversationAsync(string title)
        {
            return await PostAsync<ChatConversationDto>("/api/conversations/", new { title });
        }

        public async Task<ChatConversationDto?> GetConversationAsync(Guid conversationId)
        {
            return await GetAsync<ChatConversationDto>($"/api/conversations/{conversationId}");
        }

        public async Task<List<ChatMessageDto>?> GetMessagesAsync(Guid conversationId)
        {
            return await GetAsync<List<ChatMessageDto>>($"/api/conversations/{conversationId}/messages");
        }

        public async Task<ChatMessageDto?> AddMessageAsync(Guid conversationId, ChatMessageDto message)
        {
            return await PostAsync<ChatMessageDto>($"/api/conversations/{conversationId}/messages", message);
        }

        public async Task<ChatConversationDto?> UpdateConversationAsync(Guid conversationId, object update)
        {
            return await PatchAsync<ChatConversationDto>($"/api/conversations/{conversationId}", update);
        }

        public async Task<bool> DeleteConversationAsync(Guid conversationId)
        {
            return await DeleteAsync($"/api/conversations/{conversationId}");
        }
    }

    // DTOs
    public record AuthResponse(Guid Id, string Email, string Token);
    public record DocumentDto(Guid Id, string Title, string Status, string? ProcessingError, string? Filename = null, long FileSizeBytes = 0, DateTime CreatedAtUtc = default);
    public record ChatResponse(Guid ConversationId, string Question, string Answer, List<SourceChunk>? SourceChunks);
    public record SourceChunk(Guid DocumentId, string? DocumentTitle, double? SimilarityScore);
    public record ChatConversationDto(Guid Id, string Title, List<string> Tags, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
    public record ChatMessageDto(Guid Id, Guid ConversationId, string Role, string Content, List<SourceDoc>? SourceDocuments, DateTime CreatedAtUtc);
    public record SourceDoc(Guid DocumentId, string? DocumentTitle, double? SimilarityScore);
}
