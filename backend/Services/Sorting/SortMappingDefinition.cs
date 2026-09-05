using System.Diagnostics.CodeAnalysis;

namespace backend.Services.Sorting;

// TSource/TDestination are not used in the body by design: the closed generic type is
// itself the lookup key that SortMappingProvider resolves out of DI.
[SuppressMessage("Major Code Smell", "S2326:Unused type parameters",
    Justification = "The closed generic type identifies the mapping pair for DI lookup.")]
public sealed class SortMappingDefinition<TSource, TDestination> : ISortMappingDefinition
{
    public required SortMapping[] Mappings { get; init; }
}
