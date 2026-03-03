using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AssetTracker.Models.Reports.Import;

public class ImportUploadVm
{
    [Display(Name = "Import file")]
    public IFormFile? File { get; set; }

    public bool UpdateExisting { get; set; } = true;
    public bool SkipInvalid { get; set; } = true;
    public string? ImportKey { get; set; }
}

public class ImportRowVm
{
    public int RowNumber { get; set; }
    public Dictionary<string, string?> Raw { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ImportPreviewVm<TRow>
{
    public string ImportKey { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public List<TRow> Rows { get; set; } = new();
}

public class ImportCommitResult
{
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public string? ErrorReportKey { get; set; }
}

public class AssetImportPageVm
{
    public ImportUploadVm Upload { get; set; } = new();
    public ImportPreviewVm<ImportRowVm>? Preview { get; set; }
    public ImportCommitResult? CommitResult { get; set; }
    public string? Message { get; set; }
}

public class StaffImportPageVm
{
    public ImportUploadVm Upload { get; set; } = new();
    public ImportPreviewVm<ImportRowVm>? Preview { get; set; }
    public ImportCommitResult? CommitResult { get; set; }
    public string? Message { get; set; }
}
