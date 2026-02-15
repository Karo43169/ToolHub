namespace ToolHub.State;

public sealed class CategoryState
{
    // Na razie “na sztywno”. Później: Load z JSON/SharePoint.
    public IReadOnlyList<string> Categories { get; } = new[]
    {
        "Deployment",
        "Utilities",
        "Diagnostics",
        "HMI/SCADA",
        "PLC",
        "Scripts",
        "Documentation"
    };
}
