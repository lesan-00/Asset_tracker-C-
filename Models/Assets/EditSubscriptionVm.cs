using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Assets;

public class EditSubscriptionVm : CreateSubscriptionVm
{
    [Required]
    public int Id { get; set; }

    [StringLength(120)]
    public string? PlanOrTier { get; set; }
}
