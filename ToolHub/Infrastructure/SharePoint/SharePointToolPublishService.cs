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

        var moveRequest = new DriveItem
        {
            Name = publishedFolderName,
            ParentReference = new ItemReference
            {
                Id = applicationSourceRoot.Id
            }
        };

        var publishedFolder = await _graph
            .Drives[DriveId]
            .Items[sourceFolder.Id]
            .PatchAsync(moveRequest, cancellationToken: ct);

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

    private static string ToSafeSharePointSegment(string value)
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

        return builder
            .ToString()
            .Trim('-')
            .Normalize(NormalizationForm.FormC);
    }
}

public sealed record PublishedToolLocation(
    string FolderName,
    string FolderPath,
    string FolderUrl);
