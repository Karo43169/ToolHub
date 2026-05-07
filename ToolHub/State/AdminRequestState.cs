using ToolHub.Domain.Models;

namespace ToolHub.State;

public sealed class AdminRequestState
{
    public ToolRequestEntry? Selected { get; private set; }

    public event Action? Changed;
    public event Action? RefreshRequested;

    public void Select(ToolRequestEntry request)
    {
        Selected = request;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Selected = null;
        Changed?.Invoke();
    }

    public void RequestRefresh()
    {
        RefreshRequested?.Invoke();
    }
}