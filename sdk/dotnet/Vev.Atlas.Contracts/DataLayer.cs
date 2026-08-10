using System.Collections.Immutable;

namespace Vev.Atlas.Contracts;

/// <summary>
/// Validation helpers for the data-layer catalogue (data-area → dataset → column) carried by a
/// <see cref="LandscapeDocument"/>. Containment is expressed with <see cref="RelationshipType.PartOf"/>
/// edges; these helpers check the "every higher level points down to a concrete dataset/column"
/// invariant that JSON Schema alone cannot express (handbook 11 §1, atlas-contracts#11).
/// </summary>
public static class DataLayerContainment
{
    /// <summary>
    /// The data-layer containment errors in a resolved landscape. Every <see cref="AssetKind.Column"/>
    /// must be <see cref="RelationshipType.PartOf"/> exactly one <see cref="AssetKind.Dataset"/>, every
    /// dataset part-of exactly one <see cref="AssetKind.DataArea"/>, and every data area part-of exactly
    /// one <see cref="AssetKind.System"/>. Returns one human-readable message per violation; an empty
    /// result means the data layer is well-formed. A landscape with no data-layer assets trivially
    /// returns empty. Non-data-layer assets and other relationship types are ignored.
    /// </summary>
    /// <param name="document">The resolved landscape to check.</param>
    public static ImmutableArray<string> DataLayerContainmentErrors(this LandscapeDocument document)
    {
        var kindById = new Dictionary<string, AssetKind>(StringComparer.Ordinal);
        foreach (var asset in document.Assets)
        {
            kindById[asset.Id] = asset.Kind;
        }

        // The part-of parents declared for each source asset.
        var partOfParents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var relationship in document.Relationships)
        {
            if (relationship.Type != RelationshipType.PartOf)
            {
                continue;
            }

            if (!partOfParents.TryGetValue(relationship.FromId, out var parents))
            {
                partOfParents[relationship.FromId] = parents = [];
            }

            parents.Add(relationship.ToId);
        }

        var errors = ImmutableArray.CreateBuilder<string>();
        foreach (var asset in document.Assets)
        {
            var requiredParent = ContainingKindOf(asset.Kind);
            if (requiredParent is null)
            {
                continue;
            }

            var containingCount = 0;
            if (partOfParents.TryGetValue(asset.Id, out var parents))
            {
                foreach (var parentId in parents)
                {
                    if (kindById.TryGetValue(parentId, out var parentKind) && parentKind == requiredParent.Value)
                    {
                        containingCount++;
                    }
                }
            }

            if (containingCount != 1)
            {
                errors.Add(
                    $"{asset.Kind} '{asset.Id}' must be part-of exactly one {requiredParent.Value}, but has {containingCount}.");
            }
        }

        return errors.ToImmutable();
    }

    /// <summary>Whether a resolved landscape's data layer satisfies the containment invariant.</summary>
    /// <param name="document">The resolved landscape to check.</param>
    public static bool DataLayerContainmentIsValid(this LandscapeDocument document) =>
        document.DataLayerContainmentErrors().IsEmpty;

    /// <summary>The kind that a data-layer asset must be contained by, or null if the kind is not a data-layer kind.</summary>
    private static AssetKind? ContainingKindOf(AssetKind kind) => kind switch
    {
        AssetKind.Column => AssetKind.Dataset,
        AssetKind.Dataset => AssetKind.DataArea,
        AssetKind.DataArea => AssetKind.System,
        _ => null
    };
}
