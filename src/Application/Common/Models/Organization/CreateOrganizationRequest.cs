namespace Application.Common.Models.Organization;

public class CreateOrganizationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool MakeSelected { get; set; } = true;
}
