using System.Globalization;
using System.Text;
using AssetTracker.Data;
using AssetTracker.Models;
using AssetTracker.Models.Reports.Import;
using CsvHelper;
using CsvHelper.Configuration;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AssetTracker.Services;

public class ImportService : IImportService
{
    private const int MaxFileSizeBytes = 10 * 1024 * 1024;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(20);

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ImportService> _logger;

    public ImportService(ApplicationDbContext context, IMemoryCache cache, ILogger<ImportService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ImportPreviewVm<ImportRowVm>> ParseAssetsAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ValidateFile(file);
        var rows = await ReadRowsAsync(file, cancellationToken);
        var previewRows = new List<ImportRowVm>();
        var records = new List<AssetRecord>();

        var seenSerials = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (rowNumber, raw) in rows)
        {
            var row = new ImportRowVm
            {
                RowNumber = rowNumber,
                Raw = raw
            };

            var assetTag = GetValue(raw, "AssetTag");
            var typeText = GetValue(raw, "Type", "AssetType");
            var brand = GetValue(raw, "Brand");
            var model = GetValue(raw, "Model");
            var serial = GetValue(raw, "SerialNumber", "Serial");
            var specs = GetValue(raw, "Specifications", "Specs");
            var statusText = GetValue(raw, "Status");
            var location = GetValue(raw, "Location");
            var condition = GetValue(raw, "Condition");

            if (string.IsNullOrWhiteSpace(assetTag))
            {
                row.Errors.Add("Missing AssetTag.");
            }

            if (string.IsNullOrWhiteSpace(typeText))
            {
                row.Errors.Add("Missing Type.");
            }

            AssetType? parsedType = null;
            if (!string.IsNullOrWhiteSpace(typeText))
            {
                if (TryParseAssetType(typeText, out var typeEnum))
                {
                    parsedType = typeEnum;
                }
                else
                {
                    row.Errors.Add($"Invalid Type '{typeText}'.");
                }
            }

            AssetStatus? parsedStatus = null;
            if (!string.IsNullOrWhiteSpace(statusText))
            {
                if (TryParseAssetStatus(statusText, out var statusEnum))
                {
                    parsedStatus = statusEnum;
                }
                else
                {
                    row.Errors.Add($"Invalid Status '{statusText}'.");
                }
            }

            if (!string.IsNullOrWhiteSpace(serial))
            {
                if (seenSerials.TryGetValue(serial, out var firstRow))
                {
                    row.Errors.Add($"Duplicate SerialNumber in file (first seen on row {firstRow}).");
                }
                else
                {
                    seenSerials[serial] = rowNumber;
                }
            }

            row.IsValid = row.Errors.Count == 0;
            previewRows.Add(row);

            records.Add(new AssetRecord
            {
                Row = row,
                AssetTag = assetTag ?? string.Empty,
                AssetType = parsedType,
                Brand = brand,
                Model = model,
                SerialNumber = serial,
                Specifications = specs,
                Status = parsedStatus,
                Location = location,
                Condition = condition
            });
        }

        var preview = new ImportPreviewVm<ImportRowVm>
        {
            ImportKey = Guid.NewGuid().ToString("N"),
            TotalRows = previewRows.Count,
            ValidRows = previewRows.Count(r => r.IsValid),
            InvalidRows = previewRows.Count(r => !r.IsValid),
            Rows = previewRows
        };

        _cache.Set(GetKey("assets", preview.ImportKey), new AssetPreviewPayload(records), CacheTtl);
        return preview;
    }

    public async Task<ImportCommitResult> CommitAssetsAsync(string importKey, bool updateExisting, bool skipInvalid, string actorUserId, CancellationToken cancellationToken = default)
    {
        if (!_cache.TryGetValue(GetKey("assets", importKey), out AssetPreviewPayload? payload) || payload is null)
        {
            throw new InvalidOperationException("Preview expired or not found. Please preview the file again.");
        }

        var result = new ImportCommitResult();
        var failedRows = new List<ImportRowVm>();
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var record in payload.Records)
        {
            if (!record.Row.IsValid)
            {
                if (skipInvalid)
                {
                    result.Skipped++;
                }
                else
                {
                    result.Failed++;
                    failedRows.Add(record.Row);
                }
                continue;
            }

            try
            {
                var existing = await FindAssetAsync(record.AssetTag, record.SerialNumber, cancellationToken);
                if (existing is null)
                {
                    if (!updateExisting)
                    {
                        var duplicate = await FindAssetAsync(record.AssetTag, record.SerialNumber, cancellationToken);
                        if (duplicate is not null)
                        {
                            record.Row.Errors.Add("Asset already exists.");
                            result.Failed++;
                            failedRows.Add(record.Row);
                            continue;
                        }
                    }

                    var newAsset = new Asset
                    {
                        AssetTag = record.AssetTag,
                        AssetType = record.AssetType!.Value,
                        Brand = string.IsNullOrWhiteSpace(record.Brand) ? "Unknown" : record.Brand.Trim(),
                        Model = string.IsNullOrWhiteSpace(record.Model) ? "Unknown" : record.Model.Trim(),
                        SerialNumber = record.SerialNumber?.Trim() ?? string.Empty,
                        Specifications = string.IsNullOrWhiteSpace(record.Specifications) ? null : record.Specifications.Trim(),
                        Status = record.Status ?? AssetStatus.InStock,
                        Location = string.IsNullOrWhiteSpace(record.Location) ? "Unknown" : record.Location.Trim(),
                        Condition = string.IsNullOrWhiteSpace(record.Condition) ? "Unknown" : record.Condition.Trim(),
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Assets.Add(newAsset);
                    await _context.SaveChangesAsync(cancellationToken);
                    result.Inserted++;
                }
                else
                {
                    if (!updateExisting)
                    {
                        result.Skipped++;
                        continue;
                    }

                    existing.AssetTag = record.AssetTag;
                    existing.AssetType = record.AssetType!.Value;
                    existing.Brand = string.IsNullOrWhiteSpace(record.Brand) ? existing.Brand : record.Brand.Trim();
                    existing.Model = string.IsNullOrWhiteSpace(record.Model) ? existing.Model : record.Model.Trim();
                    existing.SerialNumber = record.SerialNumber?.Trim() ?? existing.SerialNumber;
                    existing.Specifications = string.IsNullOrWhiteSpace(record.Specifications) ? existing.Specifications : record.Specifications.Trim();
                    if (record.Status.HasValue)
                    {
                        existing.Status = record.Status.Value;
                    }
                    if (!string.IsNullOrWhiteSpace(record.Location))
                    {
                        existing.Location = record.Location.Trim();
                    }
                    if (!string.IsNullOrWhiteSpace(record.Condition))
                    {
                        existing.Condition = record.Condition.Trim();
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    result.Updated++;
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Asset import failed for row {RowNumber}.", record.Row.RowNumber);
                result.Failed++;
                record.Row.Errors.Add("Database save failed for this row.");
                failedRows.Add(record.Row);
                foreach (var entry in ex.Entries)
                {
                    entry.State = EntityState.Detached;
                }
            }
        }

        await tx.CommitAsync(cancellationToken);
        if (failedRows.Count > 0)
        {
            var key = Guid.NewGuid().ToString("N");
            _cache.Set(GetKey("errors", key), BuildErrorCsv(failedRows), CacheTtl);
            result.ErrorReportKey = key;
        }

        return result;
    }

    public async Task<ImportPreviewVm<ImportRowVm>> ParseStaffAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ValidateFile(file);
        var rows = await ReadRowsAsync(file, cancellationToken);
        var previewRows = new List<ImportRowVm>();
        var records = new List<StaffRecord>();

        var seenEmployee = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (rowNumber, raw) in rows)
        {
            var row = new ImportRowVm
            {
                RowNumber = rowNumber,
                Raw = raw
            };

            var fullName = GetValue(raw, "FullName", "Name");
            var employeeNumber = GetValue(raw, "EmployeeNumber", "EmployeeNo");
            var department = GetValue(raw, "Department");
            var phone = GetValue(raw, "PhoneNumber", "Phone");

            if (string.IsNullOrWhiteSpace(fullName))
            {
                row.Errors.Add("Missing FullName.");
            }

            if (string.IsNullOrWhiteSpace(employeeNumber))
            {
                row.Errors.Add("Missing EmployeeNumber.");
            }
            else if (seenEmployee.TryGetValue(employeeNumber, out var firstRow))
            {
                row.Errors.Add($"Duplicate EmployeeNumber in file (first seen on row {firstRow}).");
            }
            else
            {
                seenEmployee[employeeNumber] = rowNumber;
            }

            row.IsValid = row.Errors.Count == 0;
            previewRows.Add(row);
            records.Add(new StaffRecord
            {
                Row = row,
                FullName = fullName ?? string.Empty,
                EmployeeNumber = employeeNumber ?? string.Empty,
                Department = department,
                PhoneNumber = phone
            });
        }

        var preview = new ImportPreviewVm<ImportRowVm>
        {
            ImportKey = Guid.NewGuid().ToString("N"),
            TotalRows = previewRows.Count,
            ValidRows = previewRows.Count(r => r.IsValid),
            InvalidRows = previewRows.Count(r => !r.IsValid),
            Rows = previewRows
        };

        _cache.Set(GetKey("staff", preview.ImportKey), new StaffPreviewPayload(records), CacheTtl);
        return preview;
    }

    public async Task<ImportCommitResult> CommitStaffAsync(string importKey, bool updateExisting, bool skipInvalid, string actorUserId, CancellationToken cancellationToken = default)
    {
        if (!_cache.TryGetValue(GetKey("staff", importKey), out StaffPreviewPayload? payload) || payload is null)
        {
            throw new InvalidOperationException("Preview expired or not found. Please preview the file again.");
        }

        var result = new ImportCommitResult();
        var failedRows = new List<ImportRowVm>();
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var record in payload.Records)
        {
            if (!record.Row.IsValid)
            {
                if (skipInvalid)
                {
                    result.Skipped++;
                }
                else
                {
                    result.Failed++;
                    failedRows.Add(record.Row);
                }
                continue;
            }

            try
            {
                var existing = await _context.StaffProfiles
                    .FirstOrDefaultAsync(s => s.EmployeeNumber == record.EmployeeNumber, cancellationToken);

                if (existing is null)
                {
                    var staff = new StaffProfile
                    {
                        FullName = record.FullName.Trim(),
                        EmployeeNumber = record.EmployeeNumber.Trim(),
                        Department = record.Department?.Trim() ?? string.Empty,
                        PhoneNumber = record.PhoneNumber?.Trim() ?? string.Empty,
                        UserId = actorUserId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.StaffProfiles.Add(staff);
                    await _context.SaveChangesAsync(cancellationToken);
                    result.Inserted++;
                }
                else
                {
                    if (!updateExisting)
                    {
                        result.Skipped++;
                        continue;
                    }

                    existing.FullName = record.FullName.Trim();
                    existing.Department = record.Department?.Trim() ?? existing.Department;
                    existing.PhoneNumber = record.PhoneNumber?.Trim() ?? existing.PhoneNumber;
                    await _context.SaveChangesAsync(cancellationToken);
                    result.Updated++;
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Staff import failed for row {RowNumber}.", record.Row.RowNumber);
                result.Failed++;
                record.Row.Errors.Add("Database save failed for this row.");
                failedRows.Add(record.Row);
                foreach (var entry in ex.Entries)
                {
                    entry.State = EntityState.Detached;
                }
            }
        }

        await tx.CommitAsync(cancellationToken);
        if (failedRows.Count > 0)
        {
            var key = Guid.NewGuid().ToString("N");
            _cache.Set(GetKey("errors", key), BuildErrorCsv(failedRows), CacheTtl);
            result.ErrorReportKey = key;
        }

        return result;
    }

    public byte[] BuildAssetsTemplateCsv()
    {
        const string template =
            "AssetTag,Type,Brand,Model,SerialNumber,Specifications,Status,Department,Location,Condition,PurchaseDate,WarrantyEndDate\n" +
            "AT-001,Laptop,Lenovo,ThinkPad T14,SN-001,16GB RAM;512GB SSD,InStock,IT,HQ,Good,2025-01-01,2028-01-01\n";
        return Encoding.UTF8.GetBytes(template);
    }

    public byte[] BuildStaffTemplateCsv()
    {
        const string template =
            "FullName,EmployeeNumber,Department,PhoneNumber,Location\n" +
            "Jane Doe,EMP001,IT,+254700000000,HQ\n";
        return Encoding.UTF8.GetBytes(template);
    }

    public bool TryGetErrorReport(string errorReportKey, out byte[] bytes)
    {
        if (_cache.TryGetValue(GetKey("errors", errorReportKey), out byte[]? value) && value is not null)
        {
            bytes = value;
            return true;
        }

        bytes = Array.Empty<byte>();
        return false;
    }

    private static string GetKey(string type, string key) => $"import:{type}:{key}";

    private static void ValidateFile(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName);
        var supported = string.Equals(ext, ".csv", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase);
        if (!supported)
        {
            throw new InvalidOperationException("Unsupported file type. Upload .csv or .xlsx only.");
        }

        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("Uploaded file exceeds 10MB limit.");
        }
    }

    private async Task<List<(int rowNumber, Dictionary<string, string?> raw)>> ReadRowsAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(file.FileName);
        await using var stream = file.OpenReadStream();
        return string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase)
            ? await ReadExcelRowsAsync(stream, cancellationToken)
            : await ReadCsvRowsAsync(stream, cancellationToken);
    }

    private static async Task<List<(int rowNumber, Dictionary<string, string?> raw)>> ReadCsvRowsAsync(Stream stream, CancellationToken cancellationToken)
    {
        var rows = new List<(int rowNumber, Dictionary<string, string?> raw)>();
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: false);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim
        };

        using var csv = new CsvReader(reader, config);
        if (!await csv.ReadAsync())
        {
            return rows;
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        var rowNumber = 1;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            var raw = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
            {
                raw[header] = csv.GetField(header);
            }

            rows.Add((rowNumber, raw));
        }

        return rows;
    }

    private static async Task<List<(int rowNumber, Dictionary<string, string?> raw)>> ReadExcelRowsAsync(Stream stream, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var rows = new List<(int rowNumber, Dictionary<string, string?> raw)>();
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new InvalidOperationException("Excel workbook is empty.");

            var headerRow = sheet.FirstRowUsed()
                ?? throw new InvalidOperationException("Excel sheet does not contain headers.");
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
            var lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 1;
            var headers = Enumerable.Range(1, lastColumn)
                .Select(i => headerRow.Cell(i).GetString())
                .ToList();

            for (var rowNum = headerRow.RowNumber() + 1; rowNum <= lastRow; rowNum++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = sheet.Row(rowNum);
                var raw = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                var anyValue = false;
                for (var col = 1; col <= lastColumn; col++)
                {
                    var val = row.Cell(col).GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        anyValue = true;
                    }
                    raw[headers[col - 1]] = val;
                }

                if (anyValue)
                {
                    rows.Add((rowNum, raw));
                }
            }

            return rows;
        }, cancellationToken);
    }

    private static string? GetValue(Dictionary<string, string?> raw, params string[] names)
    {
        foreach (var name in names)
        {
            var normalized = NormalizeHeader(name);
            foreach (var kvp in raw)
            {
                if (NormalizeHeader(kvp.Key) == normalized)
                {
                    return string.IsNullOrWhiteSpace(kvp.Value) ? null : kvp.Value.Trim();
                }
            }
        }

        return null;
    }

    private static string NormalizeHeader(string header)
    {
        var chars = header.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private static bool TryParseAssetType(string input, out AssetType value)
    {
        var normalized = NormalizeEnumToken(input);
        foreach (var enumValue in Enum.GetValues<AssetType>())
        {
            if (NormalizeEnumToken(enumValue.ToString()) == normalized)
            {
                value = enumValue;
                return true;
            }
        }

        return Enum.TryParse(input, true, out value);
    }

    private static bool TryParseAssetStatus(string input, out AssetStatus value)
    {
        var normalized = NormalizeEnumToken(input);
        foreach (var enumValue in Enum.GetValues<AssetStatus>())
        {
            if (NormalizeEnumToken(enumValue.ToString()) == normalized)
            {
                value = enumValue;
                return true;
            }
        }

        return Enum.TryParse(input, true, out value);
    }

    private static string NormalizeEnumToken(string token)
    {
        return new string(token.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private async Task<Asset?> FindAssetAsync(string assetTag, string? serialNumber, CancellationToken cancellationToken)
    {
        var existing = await _context.Assets.FirstOrDefaultAsync(a => a.AssetTag == assetTag, cancellationToken);
        if (existing is not null || string.IsNullOrWhiteSpace(serialNumber))
        {
            return existing;
        }

        return await _context.Assets.FirstOrDefaultAsync(a => a.SerialNumber == serialNumber, cancellationToken);
    }

    private static byte[] BuildErrorCsv(IEnumerable<ImportRowVm> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RowNumber,Errors,RawData");
        foreach (var row in rows)
        {
            var raw = string.Join(" | ", row.Raw.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            var errors = string.Join("; ", row.Errors);
            sb.AppendLine($"{row.RowNumber},\"{errors.Replace("\"", "\"\"")}\",\"{raw.Replace("\"", "\"\"")}\"");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private sealed class AssetPreviewPayload(List<AssetRecord> records)
    {
        public List<AssetRecord> Records { get; } = records;
    }

    private sealed class StaffPreviewPayload(List<StaffRecord> records)
    {
        public List<StaffRecord> Records { get; } = records;
    }

    private sealed class AssetRecord
    {
        public ImportRowVm Row { get; set; } = new();
        public string AssetTag { get; set; } = string.Empty;
        public AssetType? AssetType { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public string? Specifications { get; set; }
        public AssetStatus? Status { get; set; }
        public string? Location { get; set; }
        public string? Condition { get; set; }
    }

    private sealed class StaffRecord
    {
        public ImportRowVm Row { get; set; } = new();
        public string FullName { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
