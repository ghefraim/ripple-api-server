using Application.Domain.Entities;

namespace Application.Common.Interfaces;

public record CascadeResult(
    List<CascadeImpact> Impacts,
    CascadeContext Context,
    List<string> NotificationTargets
);

public interface ICascadeEngine
{
    Task<CascadeResult> ProcessDisruptionAsync(Disruption disruption, CancellationToken cancellationToken = default);
}
