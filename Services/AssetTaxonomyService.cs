using AssetTracker.Models;
using AssetTracker.Models.Assets;
using Microsoft.AspNetCore.Mvc;

namespace AssetTracker.Services;

public sealed class AssetTaxonomyService : IAssetTaxonomyService
{
    private sealed class NodeDefinition
    {
        public required string Key { get; init; }
        public required string Name { get; init; }
        public string? Icon { get; init; }
        public IReadOnlyList<NodeDefinition> Children { get; init; } = [];
    }

    private static readonly IReadOnlyList<NodeDefinition> RootNodes =
    [
        new NodeDefinition
        {
            Key = "hardware",
            Name = "Hardware",
            Icon = "bi bi-cpu",
            Children =
            [
                new NodeDefinition { Key = "laptops", Name = "Laptops" },
                new NodeDefinition { Key = "desktops", Name = "Desktops" },
                new NodeDefinition { Key = "monitors", Name = "Monitors" },
                new NodeDefinition { Key = "printers", Name = "Printers" },
                new NodeDefinition { Key = "servers", Name = "Servers" },
                new NodeDefinition { Key = "mobile-phones", Name = "Mobile Phones" },
                new NodeDefinition { Key = "peripherals", Name = "Peripherals" }
            ]
        },
        new NodeDefinition
        {
            Key = "software",
            Name = "Software",
            Icon = "bi bi-window-stack",
            Children =
            [
                new NodeDefinition { Key = "licensed-applications", Name = "Licensed Applications" },
                new NodeDefinition { Key = "operating-systems", Name = "Operating Systems" },
                new NodeDefinition { Key = "subscriptions", Name = "Subscriptions" },
                new NodeDefinition { Key = "open-source", Name = "Open Source" }
            ]
        },
        new NodeDefinition
        {
            Key = "physical",
            Name = "Physical",
            Icon = "bi bi-box-seam"
        }
    ];

    private static readonly Dictionary<string, NodeDefinition> NodesByKey = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> ParentByKey = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> DepthByKey = new(StringComparer.OrdinalIgnoreCase);

    static AssetTaxonomyService()
    {
        foreach (var root in RootNodes)
        {
            RegisterNode(root, parentKey: null, depth: 0);
        }
    }

    public AssetTaxonomySelection InferSelectionFromAssetType(AssetType assetType)
    {
        return assetType switch
        {
            AssetType.Software => new AssetTaxonomySelection { TopLevelCategory = "software", Category = "licensed-applications" },
            AssetType.Laptop => new AssetTaxonomySelection { TopLevelCategory = "hardware", Category = "laptops" },
            AssetType.Desktop => new AssetTaxonomySelection { TopLevelCategory = "hardware", Category = "desktops" },
            AssetType.SystemUnit => new AssetTaxonomySelection { TopLevelCategory = "hardware", Category = "desktops" },
            AssetType.Monitor => new AssetTaxonomySelection { TopLevelCategory = "hardware", Category = "monitors" },
            AssetType.Printer => new AssetTaxonomySelection { TopLevelCategory = "hardware", Category = "printers" },
            AssetType.Router => new AssetTaxonomySelection { TopLevelCategory = "hardware", Category = "servers" },
            AssetType.Switch => new AssetTaxonomySelection { TopLevelCategory = "hardware", Category = "servers" },
            AssetType.MobilePhone => new AssetTaxonomySelection { TopLevelCategory = "hardware", Category = "mobile-phones" },
            AssetType.Keyboard => new AssetTaxonomySelection { TopLevelCategory = "hardware", Category = "peripherals" },
            AssetType.Chairs => new AssetTaxonomySelection { TopLevelCategory = "physical" },
            AssetType.Whiteboards => new AssetTaxonomySelection { TopLevelCategory = "physical" },
            AssetType.Desks => new AssetTaxonomySelection { TopLevelCategory = "physical" },
            AssetType.Cabinets => new AssetTaxonomySelection { TopLevelCategory = "physical" },
            AssetType.Tables => new AssetTaxonomySelection { TopLevelCategory = "physical" },
            AssetType.Drawers => new AssetTaxonomySelection { TopLevelCategory = "physical" },
            AssetType.Other => new AssetTaxonomySelection { TopLevelCategory = "physical" },
            _ => new AssetTaxonomySelection { TopLevelCategory = "hardware", Category = "peripherals" }
        };
    }

    public AssetTaxonomySelection ResolveAssetSelection(Asset asset)
    {
        if (asset.AssetType == AssetType.MobilePhone)
        {
            return InferSelectionFromAssetType(asset.AssetType);
        }

        if (TryNormalizeSelection(asset.TopLevelCategory, asset.Category, asset.SubCategory, out var normalized, out _)
            && !normalized.IsEmpty)
        {
            return normalized;
        }

        return InferSelectionFromAssetType(asset.AssetType);
    }

    public bool TryNormalizeSelection(
        string? topLevelCategory,
        string? category,
        string? subCategory,
        out AssetTaxonomySelection normalizedSelection,
        out string? validationError)
    {
        var top = NormalizeKey(topLevelCategory);
        var cat = NormalizeKey(category);
        var sub = NormalizeKey(subCategory);

        if (string.IsNullOrWhiteSpace(top) && string.IsNullOrWhiteSpace(cat) && string.IsNullOrWhiteSpace(sub))
        {
            normalizedSelection = new AssetTaxonomySelection();
            validationError = null;
            return true;
        }

        // Physical module is now single-level in navigation. Collapse legacy child selections.
        var hasLegacyPhysicalCategory = IsLegacyPhysicalKey(cat) || IsLegacyPhysicalKey(sub);
        if (string.Equals(top, "physical", StringComparison.OrdinalIgnoreCase) || hasLegacyPhysicalCategory)
        {
            if (!string.IsNullOrWhiteSpace(top) && !string.Equals(top, "physical", StringComparison.OrdinalIgnoreCase) && hasLegacyPhysicalCategory)
            {
                normalizedSelection = new AssetTaxonomySelection();
                validationError = "Selected top-level category does not match physical selection.";
                return false;
            }

            normalizedSelection = new AssetTaxonomySelection
            {
                TopLevelCategory = "physical",
                Category = null,
                SubCategory = null
            };
            validationError = null;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(sub))
        {
            if (!TryGetNodeAtDepth(sub, expectedDepth: 2, out _))
            {
                normalizedSelection = new AssetTaxonomySelection();
                validationError = "Selected subcategory is not valid.";
                return false;
            }

            var parentCategory = ParentByKey[sub];
            var parentTop = ParentByKey[parentCategory];

            if (!string.IsNullOrWhiteSpace(cat) && !string.Equals(cat, parentCategory, StringComparison.OrdinalIgnoreCase))
            {
                normalizedSelection = new AssetTaxonomySelection();
                validationError = "Selected category/subcategory combination is invalid.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(top) && !string.Equals(top, parentTop, StringComparison.OrdinalIgnoreCase))
            {
                normalizedSelection = new AssetTaxonomySelection();
                validationError = "Selected top-level category does not match subcategory.";
                return false;
            }

            cat = parentCategory;
            top = parentTop;
        }

        if (!string.IsNullOrWhiteSpace(cat))
        {
            if (!NodesByKey.TryGetValue(cat, out _))
            {
                normalizedSelection = new AssetTaxonomySelection();
                validationError = "Selected category is not valid.";
                return false;
            }

            var depth = DepthByKey[cat];
            if (depth == 2)
            {
                var parentCategory = ParentByKey[cat];
                var parentTop = ParentByKey[parentCategory];
                sub = cat;
                cat = parentCategory;
                top = parentTop;
            }
            else if (depth == 1)
            {
                var parentTop = ParentByKey[cat];
                if (!string.IsNullOrWhiteSpace(top) && !string.Equals(top, parentTop, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedSelection = new AssetTaxonomySelection();
                    validationError = "Selected top-level category does not match category.";
                    return false;
                }

                top = parentTop;
            }
            else if (depth == 0)
            {
                top = cat;
                cat = null;
                sub = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(top) && !TryGetNodeAtDepth(top, expectedDepth: 0, out _))
        {
            normalizedSelection = new AssetTaxonomySelection();
            validationError = "Selected top-level category is not valid.";
            return false;
        }

        normalizedSelection = new AssetTaxonomySelection
        {
            TopLevelCategory = top,
            Category = cat,
            SubCategory = sub
        };
        validationError = null;
        return true;
    }

    public IReadOnlyList<AssetTaxonomyOptionVm> GetOptionsTree()
    {
        return RootNodes.Select(MapOption).ToList();
    }

    public IReadOnlyList<AssetNavNodeVm> BuildNavigationTree(
        IReadOnlyList<AssetTaxonomyCountRow> rows,
        AssetTaxonomySelection selected,
        IUrlHelper urlHelper)
    {
        var counts = NodesByKey.Keys.ToDictionary(k => k, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var resolved = ResolveRowSelection(row);
            if (string.IsNullOrWhiteSpace(resolved.TopLevelCategory))
            {
                continue;
            }

            counts[resolved.TopLevelCategory!] += row.Count;
            if (!string.IsNullOrWhiteSpace(resolved.Category))
            {
                counts[resolved.Category!] += row.Count;
            }

            if (!string.IsNullOrWhiteSpace(resolved.SubCategory))
            {
                counts[resolved.SubCategory!] += row.Count;
            }
        }

        return RootNodes
            .Select(root => BuildNode(root, counts, selected, urlHelper, depth: 0, topKey: null, categoryKey: null))
            .ToList();
    }

    public IReadOnlyList<AssetBreadcrumbVm> BuildBreadcrumbs(
        AssetTaxonomySelection selected,
        IUrlHelper urlHelper)
    {
        var list = new List<AssetBreadcrumbVm>
        {
            new()
            {
                Label = "Assets",
                Url = urlHelper.Action("Index", "Assets") ?? "/Assets",
                IsActive = selected.IsEmpty
            }
        };

        if (!string.IsNullOrWhiteSpace(selected.TopLevelCategory) && NodesByKey.TryGetValue(selected.TopLevelCategory, out var top))
        {
            list.Add(new AssetBreadcrumbVm
            {
                Label = top.Name,
                Url = urlHelper.Action("Index", "Assets", new { group = top.Key }) ?? "/Assets",
                IsActive = string.IsNullOrWhiteSpace(selected.Category)
            });
        }

        if (!string.IsNullOrWhiteSpace(selected.Category) && NodesByKey.TryGetValue(selected.Category, out var category))
        {
            list.Add(new AssetBreadcrumbVm
            {
                Label = category.Name,
                Url = urlHelper.Action("Index", "Assets", new
                {
                    group = selected.TopLevelCategory,
                    category = category.Key
                }) ?? "/Assets",
                IsActive = string.IsNullOrWhiteSpace(selected.SubCategory)
            });
        }

        if (!string.IsNullOrWhiteSpace(selected.SubCategory) && NodesByKey.TryGetValue(selected.SubCategory, out var sub))
        {
            list.Add(new AssetBreadcrumbVm
            {
                Label = sub.Name,
                Url = urlHelper.Action("Index", "Assets", new
                {
                    group = selected.TopLevelCategory,
                    category = selected.Category,
                    subCategory = sub.Key
                }) ?? "/Assets",
                IsActive = true
            });
        }

        return list;
    }

    public string BuildHeading(AssetTaxonomySelection selected)
    {
        var parts = new List<string> { "Assets" };
        if (!string.IsNullOrWhiteSpace(selected.TopLevelCategory) && NodesByKey.TryGetValue(selected.TopLevelCategory, out var top))
        {
            parts.Add(top.Name);
        }

        if (!string.IsNullOrWhiteSpace(selected.Category) && NodesByKey.TryGetValue(selected.Category, out var category))
        {
            parts.Add(category.Name);
        }

        if (!string.IsNullOrWhiteSpace(selected.SubCategory) && NodesByKey.TryGetValue(selected.SubCategory, out var sub))
        {
            parts.Add(sub.Name);
        }

        return string.Join(" / ", parts);
    }

    private AssetTaxonomySelection ResolveRowSelection(AssetTaxonomyCountRow row)
    {
        var inferred = InferSelectionFromAssetType(row.AssetType);

        if (row.AssetType == AssetType.MobilePhone)
        {
            return inferred;
        }

        if (TryNormalizeSelection(row.TopLevelCategory, row.Category, row.SubCategory, out var normalized, out _)
            && !normalized.IsEmpty)
        {
            return normalized;
        }

        return inferred;
    }

    private static AssetTaxonomyOptionVm MapOption(NodeDefinition node)
    {
        return new AssetTaxonomyOptionVm
        {
            Key = node.Key,
            Name = node.Name,
            Children = node.Children.Select(MapOption).ToList()
        };
    }

    private static AssetNavNodeVm BuildNode(
        NodeDefinition node,
        IReadOnlyDictionary<string, int> counts,
        AssetTaxonomySelection selected,
        IUrlHelper urlHelper,
        int depth,
        string? topKey,
        string? categoryKey)
    {
        var children = node.Children
            .Select(child => BuildNode(
                child,
                counts,
                selected,
                urlHelper,
                depth + 1,
                depth == 0 ? node.Key : topKey,
                depth == 1 ? node.Key : categoryKey))
            .ToList();

        var isSelected = depth switch
        {
            0 => string.Equals(selected.TopLevelCategory, node.Key, StringComparison.OrdinalIgnoreCase),
            1 => string.Equals(selected.Category, node.Key, StringComparison.OrdinalIgnoreCase),
            2 => string.Equals(selected.SubCategory, node.Key, StringComparison.OrdinalIgnoreCase),
            _ => false
        };

        var hasSelectedDescendant = children.Any(c => c.IsSelected || c.HasSelectedDescendant);
        var isExpanded = isSelected || hasSelectedDescendant;
        var navigationUrl = depth switch
        {
            0 => urlHelper.Action("Index", "Assets", new { group = node.Key }) ?? "/Assets",
            1 => urlHelper.Action("Index", "Assets", new { group = topKey, category = node.Key }) ?? "/Assets",
            _ => urlHelper.Action("Index", "Assets", new { group = topKey, category = categoryKey, subCategory = node.Key }) ?? "/Assets"
        };

        return new AssetNavNodeVm
        {
            Name = node.Name,
            Key = node.Key,
            Icon = depth == 0 ? node.Icon : null,
            Count = counts.TryGetValue(node.Key, out var count) ? count : 0,
            Depth = depth,
            NodeId = $"asset-node-{node.Key}",
            NavigationUrl = navigationUrl,
            IsExpanded = isExpanded,
            IsSelected = isSelected,
            HasSelectedDescendant = hasSelectedDescendant,
            Children = children
        };
    }

    private static bool TryGetNodeAtDepth(string key, int expectedDepth, out NodeDefinition? node)
    {
        if (NodesByKey.TryGetValue(key, out node) && DepthByKey[key] == expectedDepth)
        {
            return true;
        }

        node = null;
        return false;
    }

    private static string? NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized;
    }

    private static bool IsLegacyPhysicalKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return key.Equals("furniture", StringComparison.OrdinalIgnoreCase)
               || key.Equals("fixtures", StringComparison.OrdinalIgnoreCase)
               || key.Equals("vehicles", StringComparison.OrdinalIgnoreCase)
               || key.Equals("desks", StringComparison.OrdinalIgnoreCase)
               || key.Equals("chairs", StringComparison.OrdinalIgnoreCase)
               || key.Equals("cabinets", StringComparison.OrdinalIgnoreCase);
    }

    private static void RegisterNode(NodeDefinition node, string? parentKey, int depth)
    {
        NodesByKey[node.Key] = node;
        DepthByKey[node.Key] = depth;
        if (!string.IsNullOrWhiteSpace(parentKey))
        {
            ParentByKey[node.Key] = parentKey;
        }

        foreach (var child in node.Children)
        {
            RegisterNode(child, node.Key, depth + 1);
        }
    }
}
