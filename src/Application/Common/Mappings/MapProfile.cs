using Application.Common.Models.User;
using Application.Domain.Entities;

namespace Application.Common.Mappings;

public class MapProfile : Profile
{
    public MapProfile()
    {
        // Request mappings
        CreateMap<ApplicationUser, EmailLogInRequest>().ReverseMap();
        CreateMap<ApplicationUser, EmailSignUpRequest>().ReverseMap();

        // Response mappings - using new consolidated models
        CreateMap<ApplicationUser, AuthenticationResponse>().ReverseMap();
        CreateMap<ApplicationUser, GoogleAuthenticationResponse>().ReverseMap();
        CreateMap<ApplicationUser, UserProfileResponse>().ReverseMap();
    }
}
