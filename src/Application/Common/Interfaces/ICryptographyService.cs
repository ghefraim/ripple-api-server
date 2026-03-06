namespace Application.Common.Interfaces;

public interface ICryptographyService
{
    string Encrypt(string value);
    string Decrypt(string encryptedText);
}
