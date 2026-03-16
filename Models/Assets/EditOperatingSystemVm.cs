using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Assets;

public class EditOperatingSystemVm : CreateOperatingSystemVm
{
    [Required]
    public int Id { get; set; }
}
