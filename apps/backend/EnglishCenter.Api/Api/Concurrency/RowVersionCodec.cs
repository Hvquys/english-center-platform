using EnglishCenter.Api.Api.ErrorHandling;

namespace EnglishCenter.Api.Api.Concurrency;

public static class RowVersionCodec
{
    public static string Encode(byte[] rowVersion)
    {
        return Convert.ToBase64String(rowVersion);
    }

    public static string ToETag(string rowVersion)
    {
        return $"\"{rowVersion}\"";
    }

    public static byte[] Decode(string? value, string fieldName = "RowVersion")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(fieldName, $"{fieldName} is required.");
        }

        var token = value.Trim();
        if (token.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            token = token[2..].Trim();
        }

        token = token.Trim('"');

        try
        {
            var bytes = Convert.FromBase64String(token);
            return bytes.Length == 8
                ? bytes
                : throw Invalid(fieldName, $"{fieldName} must represent an 8-byte SQL Server rowversion.");
        }
        catch (FormatException)
        {
            throw Invalid(fieldName, $"{fieldName} must be a valid Base64 value.");
        }
    }

    private static RequestValidationException Invalid(string fieldName, string message)
    {
        return new RequestValidationException(
            message,
            new Dictionary<string, string[]>
            {
                [fieldName] = [message]
            });
    }
}
