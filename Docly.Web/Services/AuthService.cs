using Microsoft.JSInterop;

namespace Docly.Web.Services
{
    public class AuthService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ApiService _apiService;
        private const string TokenKey = "docly.token";
        private const string EmailKey = "docly.email";

        public event Action? OnAuthChanged;

        public AuthService(IJSRuntime jsRuntime, ApiService apiService)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        }

        public async Task<string?> GetTokenAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TokenKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting token: {ex.Message}");
                return null;
            }
        }

        public async Task SetTokenAsync(string token)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting token: {ex.Message}");
            }
        }

        public async Task<string?> GetEmailAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", EmailKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting email: {ex.Message}");
                return null;
            }
        }

        public async Task SetEmailAsync(string email)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", EmailKey, email);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting email: {ex.Message}");
            }
        }

        public async Task ClearAuthAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", EmailKey);
                OnAuthChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing auth: {ex.Message}");
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            var token = await GetTokenAsync();
            return !string.IsNullOrEmpty(token);
        }

        public async Task<AuthResponse?> LoginAsync(string email, string password)
        {
            try
            {
                var response = await _apiService.LoginAsync(email, password);
                if (response != null)
                {
                    await SetTokenAsync(response.Token);
                    await SetEmailAsync(response.Email);
                    OnAuthChanged?.Invoke();
                }
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                throw;
            }
        }

        public async Task<AuthResponse?> RegisterAsync(string email, string password)
        {
            try
            {
                var response = await _apiService.RegisterAsync(email, password);
                if (response != null)
                {
                    await SetTokenAsync(response.Token);
                    await SetEmailAsync(response.Email);
                    OnAuthChanged?.Invoke();
                }
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Register error: {ex.Message}");
                throw;
            }
        }

        public async Task LogoutAsync()
        {
            await ClearAuthAsync();
        }
    }
}
