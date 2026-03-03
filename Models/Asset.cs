using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Models;

public enum AssetType
{
    Laptop,
    Printer,
    Switch,
    Router,
    Desktop,
    Monitor,
    Keyboard,
    [Display(Name = "Mobile Phone")]
    MobilePhone,
    [Display(Name = "System Unit")]
    SystemUnit,
    [Display(Name = "PDA")]
    Pda,
    [Display(Name = "Hcs Crane Scale")]
    HcsCraneScale
}

[Index(nameof(AssetTag), IsUnique = true)]
public class Asset
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string AssetTag { get; set; } = string.Empty;

    [StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    [Required]
    public AssetType AssetType { get; set; } = AssetType.Laptop;

    [Required]
    [StringLength(100)]
    public string Brand { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Model { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Specifications { get; set; }

    [Required]
    public AssetStatus Status { get; set; } = AssetStatus.InStock;

    [Required]
    [StringLength(100)]
    public string Location { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Condition { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum AssetStatus
{
    InStock,
    Assigned,
    InRepair,
    Retired
}
