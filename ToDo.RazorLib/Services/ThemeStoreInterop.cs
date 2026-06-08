using Microsoft.JSInterop;

namespace ToDo.RazorLib.Services;

public sealed class ThemeStoreInterop {
    private readonly IJSRuntime _js;

    public ThemeStoreInterop(IJSRuntime js) {
        _js = js;
    }

    public async ValueTask PersistAsync(string primary, string background, bool isDark) {
        await _js.InvokeVoidAsync("themeStore.persist", primary, background, isDark.ToString().ToLower());
    }
}