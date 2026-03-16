using System.ComponentModel.DataAnnotations;

namespace AssetTracker.Models.Assets;

public class EditLicensedApplicationVm : CreateLicensedApplicationVm
{
    [Required]
    public int Id { get; set; }
}
