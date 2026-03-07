using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Gates.CreateGate;

[Authorize(Roles = "Owner")]
public record CreateGateCommand(string Code, GateType GateType, GateSizeCategory SizeCategory) : IRequest<CreateGateResponse>;

public record CreateGateResponse(
    Guid Id,
    string Code,
    GateType Type,
    GateSizeCategory SizeCategory,
    bool IsActive
);

public class CreateGateCommandValidator : AbstractValidator<CreateGateCommand>
{
    public CreateGateCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.");
    }
}

public class CreateGateCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<CreateGateCommand, CreateGateResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<CreateGateResponse> Handle(CreateGateCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var airport = await _context.AirportConfigs
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId, cancellationToken)
            ?? throw new NotFoundException("AirportConfig", organizationId);

        var gate = new Gate
        {
            OrganizationId = organizationId,
            AirportId = airport.Id,
            Code = request.Code,
            GateType = request.GateType,
            SizeCategory = request.SizeCategory,
            IsActive = true,
        };

        _context.Gates.Add(gate);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateGateResponse(
            gate.Id,
            gate.Code,
            gate.GateType,
            gate.SizeCategory,
            gate.IsActive);
    }
}
