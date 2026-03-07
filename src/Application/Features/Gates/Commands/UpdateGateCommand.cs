using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Gates.UpdateGate;

[Authorize(Roles = "Owner")]
public record UpdateGateCommand(
    Guid Id,
    string Code,
    GateType GateType,
    GateSizeCategory SizeCategory,
    bool IsActive
) : IRequest<UpdateGateResponse>;

public record UpdateGateResponse(
    Guid Id,
    string Code,
    GateType Type,
    GateSizeCategory SizeCategory,
    bool IsActive
);

public class UpdateGateCommandValidator : AbstractValidator<UpdateGateCommand>
{
    public UpdateGateCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.");
    }
}

public class UpdateGateCommandHandler(
    ApplicationDbContext context) : IRequestHandler<UpdateGateCommand, UpdateGateResponse>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<UpdateGateResponse> Handle(UpdateGateCommand request, CancellationToken cancellationToken)
    {
        var gate = await _context.Gates
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Gate), request.Id);

        gate.Code = request.Code;
        gate.GateType = request.GateType;
        gate.SizeCategory = request.SizeCategory;
        gate.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateGateResponse(
            gate.Id,
            gate.Code,
            gate.GateType,
            gate.SizeCategory,
            gate.IsActive);
    }
}
