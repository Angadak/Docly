using Microsoft.JSInterop;

namespace Docly.Web.Services
{
    public class ThemeService
    {
        private readonly IJSRuntime _jsRuntime;
        public event Action<string>? OnThemeChanged;
        public string CurrentTheme { get; private set; } = "dark";

        public ThemeService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }

        public async Task InitializeAsync()
        {
            try
            {
                // init() falls back to the OS preference when nothing is saved, matching what theme.js applied.
                var theme = await _jsRuntime.InvokeAsync<string>("doclyTheme.init");
                CurrentTheme = string.IsNullOrEmpty(theme) ? "light" : theme;
            }
            catch
            {
                CurrentTheme = "light";
            }
        }

        public async Task SetThemeAsync(string theme)
        {
            CurrentTheme = theme;
            try
            {
                await _jsRuntime.InvokeVoidAsync("doclyTheme.set", theme);
            }
            catch
            {
                // ignore JS interop errors (e.g. prerendering)
            }
            OnThemeChanged?.Invoke(theme);
        }

        public Task ToggleAsync() => SetThemeAsync(CurrentTheme == "dark" ? "light" : "dark");
    }
}
