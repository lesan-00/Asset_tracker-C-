namespace AssetTracker.Models.Procurement;

public sealed class ProcurementPipelineVm
{
    public string ActiveFilter { get; init; } = "all";
    public string? Query { get; init; }

    public int PrCount { get; init; }
    public int PoCount { get; init; }
    public int InvoiceCount { get; init; }

    public decimal YtdSpend { get; init; }
    public int PendingPrCount { get; init; }
    public int OpenPoCount { get; init; }
    public int PendingInvoiceCount { get; init; }

    public IReadOnlyList<ProcurementStepCardVm> StepCards { get; init; } = [];
    public IReadOnlyList<ProcurementFilterTabVm> FilterTabs { get; init; } = [];
    public IReadOnlyList<ProcurementUnifiedDocumentVm> UnifiedDocuments { get; init; } = [];
}

public sealed class ProcurementStepCardVm
{
    public int StepNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public int Count { get; init; }
    public string? LatestDocumentNumber { get; init; }
    public string Icon { get; init; } = "bi bi-circle";
}

public sealed class ProcurementFilterTabVm
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    public bool IsActive { get; init; }
}

public sealed class ProcurementUnifiedDocumentVm
{
    public string DocumentTypeKey { get; init; } = string.Empty;
    public string DocumentTypeLabel { get; init; } = string.Empty;
    public int DocumentId { get; init; }
    public string DocumentNumber { get; init; } = string.Empty;
    public DateTime DocumentDate { get; init; }
    public string VendorOrRequester { get; init; } = "-";
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusBadgeClass { get; init; } = "text-bg-secondary";
    public decimal? Amount { get; init; }
    public string Currency { get; init; } = "KES";
    public string LinkedDocument { get; init; } = "-";
}
