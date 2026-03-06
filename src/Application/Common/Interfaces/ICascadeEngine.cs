using Application.Domain.Entities;

namespace Application.Common.Interfaces;

public record CascadeResult(
    List<CascadeImpact> Impacts,
    CascadeContext Context
);

public interface ICascadeEngine
{
    Task<CascadeResult> ProcessDisruptionAsync(Disruption disruption, CancellationToken cancellationToken = default);
}
