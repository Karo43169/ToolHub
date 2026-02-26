using Microsoft.JSInterop;

namespace ToolHub.State;

public sealed class ThemeState
{
    private readonly IJSRuntime _js;

    public ThemeState(IJSRuntime js)
    {
        _js = js;
    }

    public string Current { get; private set; } = "light";

    public async Task InitializeAsync()
    {
        // Odczyt z localStorage i od razu ustawienie klasy na body
        Current = await _js.InvokeAsync<string>("toolhubTheme.get");
        await _js.InvokeVoidAsync("toolhubTheme.set", Current);
    }

    public async Task ToggleAsync()
    {
        Current = (Current == "dark") ? "light" : "dark";
        await _js.InvokeVoidAsync("toolhubTheme.set", Current);
    }
}
