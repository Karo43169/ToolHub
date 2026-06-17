using System.Globalization;
using System.Text;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using ToolHub.Domain.Models;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointToolPublishService
{
    private readonly GraphServiceClient _graph;

    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    private const string ApplicationSourceRoot =
        "Platform Components/Application Source";

    public SharePointToolPublishService(GraphServiceClient graph)
    {
        _graph = graph;
    }

    public async Task<PublishedToolLocation> PublishAsync(
        ToolRequestEntry request,
        string toolId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(toolId))
            throw new ArgumentException("Tool id cannot be empty.", nameof(toolId));

        if (string.IsNullOrWhiteSpace(request.ApplicationReqFolder))
            throw new InvalidOperationException("Request does not contain ApplicationReqFolder path.");

        var sourceFolderPath = NormalizePath(request.ApplicationReqFolder);
        var publishedFolderName = BuildPublishedFolderName(toolId, request.Tool.Name);
        var publishedFolderPath = $"{ApplicationSourceRoot}/{publishedFolderName}";

        var sourceFolder = await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath(sourceFolderPath)
            .GetAsync(cancellationToken: ct);

        if (sourceFolder?.Id is null)
            throw new InvalidOperationException($"Source request folder was not found: {sourceFolderPath}");

        var applicationSourceRoot = await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath(ApplicationSourceRoot)
            .GetAsync(cancellationToken: ct);

        if (applicationSourceRoot?.Id is null)
            throw new InvalidOperationException($"Application Source folder was not found: {ApplicationSourceRoot}");

        // Sanityzuj nazwę folderu ostatecznie przed przeniesieniem (usuń niedozwolone znaki, końcowe kropki itp.)
        var finalPublishedFolderName = ToSafeSharePointSegment(publishedFolderName);
        if (string.IsNullOrWhiteSpace(finalPublishedFolderName))
            finalPublishedFolderName = "tool";

        var moveRequest = new DriveItem
        {
            Name = finalPublishedFolderName,
            ParentReference = new ItemReference
            {
                Id = applicationSourceRoot.Id
            }
        };

        DriveItem? publishedFolder = null;
        try
        {
            publishedFolder = await _graph
                .Drives[DriveId]
                .Items[sourceFolder.Id]
                .PatchAsync(moveRequest, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Rzucamy bardziej opisowy wyjątek by front-end mógł pokazać sensowny komunikat i logi były czytelne.
            throw new InvalidOperationException($"Failed to move request folder to published location. Name='{finalPublishedFolderName}'. See inner exception for details.", ex);
        }

        return new PublishedToolLocation(
            FolderName: publishedFolderName,
            FolderPath: publishedFolderPath,
            FolderUrl: publishedFolder?.WebUrl ?? string.Empty);
    }

    private static string BuildPublishedFolderName(string toolId, string toolName)
    {
        var shortId = toolId.Length >= 8
            ? toolId[..8]
            : toolId;

        var safeName = ToSafeSharePointSegment(toolName);

        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "tool";

        return $"{shortId}-{safeName}";
    }

    private static string NormalizePath(string path)
    {
        return path
            .Replace('\\', '/')
            .Trim()
            .Trim('/');
    }

    public static string ToSafeSharePointSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);

            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousWasSeparator = false;
                continue;
            }

            if (ch is '-' or '_' or '.')
            {
                builder.Append(ch);
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        // Final cleanup: remove leading/trailing separators and trailing dots/spaces which SharePoint rejects.
        var result = builder
            .ToString()
            .Trim('-')
            .Normalize(NormalizationForm.FormC);

        // Remove trailing dots and spaces (SharePoint disallows names ending with a dot or space)
        result = result.TrimEnd(' ', '.');

        // Collapse multiple dots at the end or multiple separators
        while (result.EndsWith(".."))
            result = result.TrimEnd('.');

        // If result is empty now, fallback to safe token
        if (string.IsNullOrWhiteSpace(result))
            return string.Empty;

        // Trim length to a safe maximum (128)
        if (result.Length > 128)
            result = result.Substring(0, 128).TrimEnd(' ', '.');

        return result;
    }
}

public sealed record PublishedToolLocation(
    string FolderName,
    string FolderPath,
    string FolderUrl);
