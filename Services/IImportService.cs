using Microsoft.AspNetCore.Http;
using AssetTracker.Models.Reports.Import;

namespace AssetTracker.Services;

public interface IImportService
{
    Task<ImportPreviewVm<ImportRowVm>> ParseAssetsAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<ImportCommitResult> CommitAssetsAsync(string importKey, bool updateExisting, bool skipInvalid, string actorUserId, CancellationToken cancellationToken = default);

    Task<ImportPreviewVm<ImportRowVm>> ParseStaffAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<ImportCommitResult> CommitStaffAsync(string importKey, bool updateExisting, bool skipInvalid, string actorUserId, CancellationToken cancellationToken = default);

    byte[] BuildAssetsTemplateCsv();
    byte[] BuildStaffTemplateCsv();
    bool TryGetErrorReport(string errorReportKey, out byte[] bytes);
}
