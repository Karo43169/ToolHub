using Microsoft.JSInterop;

namespace ToolHub.State;

public sealed class ThemeState
{
    private readonly IJSRuntime _js;

    public ThemeState(IJSRuntime js)
    {
        _js = js;
    }

    public string Current { get; private set; } = "dark";
    public bool IsLight => Current == "light";
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        // odczyt z localStorage
        var saved = await _js.InvokeAsync<string>("toolhubTheme.get");

        // zabezpieczenie na różne formaty
        saved = (saved ?? "").Trim().ToLowerInvariant();
        Current = (saved == "light") ? "light" : "dark";

        await _js.InvokeVoidAsync("toolhubTheme.set", Current);

        Changed?.Invoke();
    }

    public async Task ToggleAsync()
    {
        Current = (Current == "dark") ? "light" : "dark";
        await _js.InvokeVoidAsync("toolhubTheme.set", Current);

        Changed?.Invoke();
    }

}
