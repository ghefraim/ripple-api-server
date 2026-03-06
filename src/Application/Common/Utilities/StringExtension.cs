using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

using Unidecode.NET;

public static class StringExtension
{
    public static string PhoneValidRegex = @"^[ 0-9\.\+\-\(\)]*$";

    /// <summary>
    /// Returns a byte array containing the hash of the value string using SHA256.
    /// </summary>
    /// <param name="value">Input string.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static byte[] GetHash(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        using (HashAlgorithm algorithm = SHA256.Create())
        {
            return algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
        }
    }

    /// <summary>
    /// Returns a string containing the hash of the value string using SHA256.
    /// </summary>
    /// <param name="value">Input string.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static string GetHashString(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        StringBuilder sb = new StringBuilder();
        foreach (byte b in GetHash(value))
        {
            sb.Append(b.ToString("X2"));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Checks if the whole string is uppercase.
    /// </summary>
    /// <param name="value">Input string.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static bool IsUppercase(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        // Consider string to be uppercase if it has no lowercase letters.
        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsLower(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Translates cyrilic, greek and coptic characters to standard characters.
    /// </summary>
    /// <param name="value">Input string.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static string TranslateCyrilicCharacters(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        // Check if there is at least one cyrillic character in the string
        if (value.ContainsCyrilic() || value.ContainsGreekAndCoptic())
        {
            // decode
            value = value.Unidecode();
        }

        return value;
    }

    /// <summary>
    /// Checks if the value string contains Cyrilic characters.
    /// </summary>
    /// <param name="value">Input string.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static bool ContainsCyrilic(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return Regex.IsMatch(value, @"\p{IsCyrillic}");
    }

    /// <summary>
    /// Checks if the value string contains Greek or Coptic characters.
    /// </summary>
    /// <param name="value">Input string.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static bool ContainsGreekAndCoptic(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return Regex.IsMatch(value, @"\p{IsGreekandCoptic}");
    }

    /// <summary>
    /// Replaces diacritics with standard characters.
    /// </summary>
    /// <param name="value">Input string.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static string ReplaceDiacritics(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var normalizedString = value.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Removes all non printable characters.
    /// </summary>
    /// <param name="value">Input string.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static string RemoveNonPrintableCharacters(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return Regex.Replace(value, @"\p{C}+", string.Empty);
    }

    /// <summary>
    /// Truncate string to max length.
    /// </summary>
    /// <param name="value">Input string.</param>
    /// <param name="maxLength">Max number of characters.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static string Truncate(this string value, int maxLength)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    /// <summary>
    /// Removes backslashes and quotes from string.
    /// </summary>
    /// <param name="value">Input string.</param>
    public static string RemoveBackslashesAndQuotes(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return Regex.Replace(value, "(\\\\|\")", string.Empty);
    }

    /// <summary>
    /// Removes backslashes from string.
    /// </summary>
    /// <param name="value">Input string.</param>
    public static string RemoveBackslashes(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return Regex.Replace(value, "(\\\\)", string.Empty);
    }

    /// <summary>
    /// Replaces invalid file characters with a givven character.
    /// </summary>
    /// <param name="filename">Input file name.</param>
    /// <param name="replaceChar">Input string that will replace invalid characters. Can be null.</param>
    /// <exception cref="ArgumentNullException">Throws exception if file name is null.</exception>
    public static string ReplaceInvalidFilePathCharacters(this string filename, string replaceChar)
    {
        if (filename is null)
        {
            throw new ArgumentNullException(nameof(filename));
        }

        string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        Regex r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
        return r.Replace(filename, replaceChar);
    }

    /// <summary>
    /// Check if string is Base64.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static bool IsBase64String(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        value = value.Trim();
        return (value.Length % 4 == 0) && Regex.IsMatch(value, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);
    }

    /// <summary>
    /// Decode Base64 string to string.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static string DecodeBase64ToString(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        value = value.Trim();
        return Encoding.UTF8.GetString(value.DecodeBase64ToByteArray());
    }

    /// <summary>
    /// Decode Base64 string to byte array.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static byte[] DecodeBase64ToByteArray(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        value = value.Trim();
        return Convert.FromBase64String(value);
    }

    /// <summary>
    /// Check if string is formatted as a Json.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static bool IsJson(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        value = value.Trim();

        try
        {
            // Try to parse the string
            var obj = JToken.Parse(value);

            // Parsing was ok if no exception was thrown
            return true;
        }
        catch
        {
            // Parsing failed
            return false;
        }
    }

    /// <summary>
    /// Check if a string value is formatted as a valid phone number.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static bool IsValidPhoneNumber(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return Regex.IsMatch(value, PhoneValidRegex, RegexOptions.None);
    }

    /// <summary>
    /// Convert string to memory stream.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <param name="encoding">Target encoding, UTF8 is used if encoding is null.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static Stream ToStream(this string value, Encoding? encoding = null)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (encoding is null)
        {
            encoding = Encoding.UTF8;
        }

        return new MemoryStream(encoding.GetBytes(value ?? string.Empty));
    }

    /// <summary>
    /// Convert string to memory stream.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <param name="encoding">Target encoding, ASCIIEncoding is used if encoding is null.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static byte[] ToByteArray(this string value, Encoding? encoding = null)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (encoding is null)
        {
            encoding = new ASCIIEncoding();
        }

        return encoding.GetBytes(value);
    }

    /// <summary>
    /// Remove all whitespace chars from string.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <exception cref="ArgumentNullException">Throws exception if value is null.</exception>
    public static string RemoveWhitespaces(this string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return new string(value.ToCharArray()
            .Where(c => !char.IsWhiteSpace(c))
            .ToArray());
    }

    /// <summary>
    /// Sanitizes a string by HTML encoding potentially dangerous characters.
    /// This prevents XSS attacks by encoding &lt;, &gt;, &amp;, ", and ' characters.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <returns>HTML-encoded string safe for display, or null if input is null.</returns>
    public static string? SanitizeHtml(this string? value)
    {
        if (value is null)
        {
            return null;
        }

        return HtmlEncoder.Default.Encode(value);
    }

    /// <summary>
    /// Checks if a string contains potentially dangerous HTML/script content.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <returns>True if the string contains HTML tags or script content.</returns>
    public static bool ContainsHtmlOrScript(this string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // Check for common XSS patterns
        var dangerousPatterns = new[]
        {
            @"<[^>]*>",                    // Any HTML tag
            @"javascript\s*:",             // JavaScript protocol
            @"on\w+\s*=",                  // Event handlers like onclick=, onerror=
            @"&#",                         // HTML entity encoding
            @"\\x[0-9a-fA-F]{2}",          // Hex encoding
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
