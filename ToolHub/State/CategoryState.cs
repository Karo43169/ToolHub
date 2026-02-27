namespace ToolHub.State;

public sealed class CategoryState
{
    public IReadOnlyList<string> Categories { get; } = new[]
    {
        "Backup / Restore Tools",
        "Data Processing",
        "Diagnostics",
        "Documentation",
        "DXQ",
        "Firmware / Hardware Tools",
        "Manuals",
        "MES",
        "Motion / Drives",
        "Network / Communication",
        "PLC",
        "Project Templates",
        "Scripts / Excel Tools",
        "Simulation",
        "Testing"
    };
}
