namespace ToolHub.State;

public enum AdminRequestViewMode
{
    Pending,
    History
}

public sealed class AdminRequestViewState
{
    public AdminRequestViewMode Current { get; private set; }
        = AdminRequestViewMode.Pending;

    public event Action? Changed;

    public bool IsPending => Current == AdminRequestViewMode.Pending;
    public bool IsHistory => Current == AdminRequestViewMode.History;

    public void SetPending()
    {
        if (Current == AdminRequestViewMode.Pending)
            return;

        Current = AdminRequestViewMode.Pending;
        Changed?.Invoke();
    }

    public void SetHistory()
    {
        if (Current == AdminRequestViewMode.History)
            return;

        Current = AdminRequestViewMode.History;
        Changed?.Invoke();
    }

    public void Toggle()
    {
        Current = Current == AdminRequestViewMode.Pending
            ? AdminRequestViewMode.History
            : AdminRequestViewMode.Pending;

        Changed?.Invoke();
    }
}
