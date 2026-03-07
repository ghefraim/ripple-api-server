using Application.Domain.Entities;

namespace Application.Common.Interfaces;

public interface IActionPlanGenerator
{
    Task<ActionPlan> GenerateAsync(Disruption disruption, CascadeResult cascadeResult, bool useLlm = true, CancellationToken cancellationToken = default);
}
