using System.Security.Cryptography;
using System.Text;

using Application.Common.Interfaces;

using Microsoft.Extensions.Configuration;

namespace Application.Infrastructure.Services;

public class CryptographyService : ICryptographyService
{
    private readonly string _secretKey;

    public CryptographyService(IConfiguration configuration)
    {
        _secretKey = configuration.GetSection("CryptographyConfiguration").GetValue<string>("SecretKey");
    }

    public string Encrypt(string value)
    {
        byte[] key = Encoding.UTF8.GetBytes(_secretKey);
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = key;
            aesAlg.Mode = CipherMode.ECB;
            aesAlg.Padding = PaddingMode.PKCS7;

            using (ICryptoTransform encryptor = aesAlg.CreateEncryptor())
            {
                byte[] plainTextBytes = Encoding.UTF8.GetBytes(value);
                byte[] cipherTextBytes = encryptor.TransformFinalBlock(plainTextBytes, 0, plainTextBytes.Length);
                return Convert.ToBase64String(cipherTextBytes);
            }
        }
    }

    public string Decrypt(string encryptedText)
    {
        byte[] key = Encoding.UTF8.GetBytes(_secretKey);
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = key;
            aesAlg.Mode = CipherMode.ECB;
            aesAlg.Padding = PaddingMode.PKCS7;

            using (ICryptoTransform decryptor = aesAlg.CreateDecryptor())
            {
                byte[] cipherTextBytes = Convert.FromBase64String(encryptedText);
                byte[] decryptedBytes = decryptor.TransformFinalBlock(cipherTextBytes, 0, cipherTextBytes.Length);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
        }
    }
}
