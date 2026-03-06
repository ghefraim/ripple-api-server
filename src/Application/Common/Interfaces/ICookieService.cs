namespace Application.Common.Interfaces;

public interface ICookieService
{
    void SetRefreshToken(string token);
    string? GetRefreshToken();
    void DeleteRefreshToken();
}

