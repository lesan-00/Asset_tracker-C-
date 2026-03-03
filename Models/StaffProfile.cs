using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace AssetTracker.Models;

public class StaffProfile
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [StringLength(100)]
    public string Department { get; set; } = string.Empty;

    [StringLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public IdentityUser? User { get; set; }
}
