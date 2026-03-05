namespace AssetTracker.Models;

public class SearchResultsVm
{
    public string Query { get; set; } = string.Empty;
    public List<SearchAssetRowVm> Assets { get; set; } = new();
    public List<SearchStaffRowVm> Staff { get; set; } = new();
}

public class SearchAssetRowVm
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class SearchStaffRowVm
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}
