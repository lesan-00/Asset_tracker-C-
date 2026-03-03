using AssetTracker.Data;
using AssetTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AssetTracker.Controllers;

[Authorize]
public class StaffController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public StaffController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index()
    {
        var profiles = await _context.StaffProfiles
            .Include(s => s.User)
            .OrderBy(s => s.FullName)
            .ToListAsync();

        return View(profiles);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var profile = await _context.StaffProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (profile is null)
        {
            return NotFound();
        }

        return View(profile);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        await PopulateUserEmailSuggestionsAsync();
        ViewData["SelectedUserEmail"] = string.Empty;
        ViewData["AssignedAssetText"] = "None";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(string userEmail, [Bind("Id,FullName,EmployeeNumber,Department,PhoneNumber")] StaffProfile profile)
    {
        IdentityUser? user = null;
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            ModelState.AddModelError(nameof(StaffProfile.UserId), "Identity user email is required.");
        }
        else
        {
            user = await _userManager.FindByEmailAsync(userEmail.Trim());
            if (user is null)
            {
                ModelState.AddModelError(nameof(StaffProfile.UserId), "No identity user found for that email.");
            }
            else
            {
                profile.UserId = user.Id;
                ModelState.Remove(nameof(StaffProfile.UserId));
            }
        }

        if (user is not null)
        {
            var duplicateProfile = await _context.StaffProfiles.AnyAsync(s => s.UserId == user.Id);
            if (duplicateProfile)
            {
                ModelState.AddModelError(nameof(StaffProfile.UserId), "This user already has a staff profile.");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateUserEmailSuggestionsAsync(userEmail);
            ViewData["SelectedUserEmail"] = userEmail ?? string.Empty;
            ViewData["AssignedAssetText"] = await GetAssignedAssetTextByEmailAsync(userEmail);
            return View(profile);
        }

        profile.CreatedAt = DateTime.UtcNow;
        _context.Add(profile);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var profile = await _context.StaffProfiles.FindAsync(id);
        if (profile is null)
        {
            return NotFound();
        }

        await PopulateUsersDropDownAsync(profile.UserId);
        return View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,UserId,FullName,EmployeeNumber,Department,PhoneNumber,CreatedAt")] StaffProfile profile)
    {
        if (id != profile.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await PopulateUsersDropDownAsync(profile.UserId);
            return View(profile);
        }

        var userExists = await _userManager.FindByIdAsync(profile.UserId) is not null;
        if (!userExists)
        {
            ModelState.AddModelError(nameof(StaffProfile.UserId), "Selected user does not exist.");
            await PopulateUsersDropDownAsync(profile.UserId);
            return View(profile);
        }

        var duplicate = await _context.StaffProfiles.AnyAsync(s => s.UserId == profile.UserId && s.Id != profile.Id);
        if (duplicate)
        {
            ModelState.AddModelError(nameof(StaffProfile.UserId), "This user already has a staff profile.");
            await PopulateUsersDropDownAsync(profile.UserId);
            return View(profile);
        }

        try
        {
            _context.Update(profile);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.StaffProfiles.AnyAsync(s => s.Id == profile.Id))
            {
                return NotFound();
            }

            throw;
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var profile = await _context.StaffProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (profile is null)
        {
            return NotFound();
        }

        return View(profile);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var profile = await _context.StaffProfiles.FindAsync(id);
        if (profile is not null)
        {
            _context.StaffProfiles.Remove(profile);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> MyProfile()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var profile = await _context.StaffProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (profile is null)
        {
            return NotFound();
        }

        return View(profile);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> LookupAssignedAsset(string? email)
    {
        var assignedAsset = await GetAssignedAssetTextByEmailAsync(email);
        return Json(new { assignedAsset });
    }

    private async Task PopulateUsersDropDownAsync(string? selectedUserId = null)
    {
        var users = await _userManager.Users
            .OrderBy(u => u.Email)
            .Select(u => new
            {
                u.Id,
                Email = u.Email ?? u.UserName ?? "(no email)"
            })
            .ToListAsync();

        ViewData["UserId"] = new SelectList(users, "Id", "Email", selectedUserId);
    }

    private async Task PopulateUserEmailSuggestionsAsync(string? selectedEmail = null)
    {
        var userEmails = await _userManager.Users
            .OrderBy(u => u.Email)
            .Select(u => u.Email ?? u.UserName ?? string.Empty)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .ToListAsync();

        ViewData["UserEmails"] = userEmails;
        ViewData["SelectedUserEmail"] = selectedEmail ?? string.Empty;
    }

    private async Task<string> GetAssignedAssetTextByEmailAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "None";
        }

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return "None";
        }

        var staffProfileId = await _context.StaffProfiles
            .Where(s => s.UserId == user.Id)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();

        if (staffProfileId is null)
        {
            return "None";
        }

        var assignedAssetTag = await _context.Assignments
            .Include(a => a.Asset)
            .Where(a => a.StaffProfileId == staffProfileId.Value
                && (a.Status == AssignmentStatus.Active
                    || a.Status == AssignmentStatus.PendingAcceptance
                    || a.Status == AssignmentStatus.Accepted
                    || a.Status == AssignmentStatus.ReturnRequested))
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => a.Asset.AssetTag)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(assignedAssetTag) ? "None" : assignedAssetTag;
    }
}
