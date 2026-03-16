using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Assets;

public class EditOpenSourceVm : CreateOpenSourceVm
{
    [Required]
    public int Id { get; set; }
}
