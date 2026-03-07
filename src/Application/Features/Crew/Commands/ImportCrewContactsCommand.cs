using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Common.Utilities;
using Application.Infrastructure.Persistence;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Crew.ImportCrewContacts;

[Authorize(Roles = "Owner")]
public record ImportCrewContactsCommand(Guid CrewId, IFormFile File) : IRequest<ImportCrewContactsResponse>;

public record ImportCrewContactsResponse(int Imported, int Skipped, List<ImportContactError> Errors);

public record ImportContactError(int Row, string Field, string Message);

public class ImportCrewContactsCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<ImportCrewContactsCommand, ImportCrewContactsResponse>
{
    private static readonly Dictionary<string, string[]> HeaderAliases = new()
    {
        ["name"] = ["name", "fullname", "full_name", "employee", "contact", "member"],
        ["phonenumber"] = ["phonenumber", "phone_number", "phone", "mobile", "tel", "telephone"],
    };

    public async Task<ImportCrewContactsResponse> Handle(ImportCrewContactsCommand request, CancellationToken cancellationToken)
    {
        var organizationId = currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var crew = await context.GroundCrews
            .FirstOrDefaultAsync(c => c.Id == request.CrewId, cancellationToken)
            ?? throw new NotFoundException(nameof(GroundCrew), request.CrewId);

        var rows = ExcelParser.ParseExcelFile(request.File, HeaderAliases);

        var errors = new List<ImportContactError>();
        var contacts = new List<CrewContact>();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNum = i + 2;

            var name = row.GetValueOrDefault("name")?.Trim() ?? "";
            var phone = row.GetValueOrDefault("phonenumber")?.Trim() ?? "";

            if (string.IsNullOrEmpty(name))
            {
                errors.Add(new ImportContactError(rowNum, "Name", "Name is required"));
                continue;
            }

            if (string.IsNullOrEmpty(phone))
            {
                errors.Add(new ImportContactError(rowNum, "PhoneNumber", "Phone number is required"));
                continue;
            }

            contacts.Add(new CrewContact
            {
                OrganizationId = organizationId,
                CrewId = crew.Id,
                Name = name,
                PhoneNumber = phone,
            });
        }

        if (contacts.Count > 0)
        {
            await context.CrewContacts.AddRangeAsync(contacts, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        return new ImportCrewContactsResponse(contacts.Count, errors.Count, errors);
    }
}
