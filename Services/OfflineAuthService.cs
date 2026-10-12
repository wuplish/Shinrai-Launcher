using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ShinraiLauncher.Services;

public static class OfflineAuthService
{
    /// <summary>
    /// Minecraft standardına (%100 java.util.UUID.nameUUIDFromBytes("OfflinePlayer:" + username) uyumlu)
    /// Offline/Cracked UUID v3 üretir.
    /// </summary>
    public static string GenerateOfflineUuid(string username, bool dashed = true)
    {
        if (string.IsNullOrWhiteSpace(username))
            username = "ShinraiPlayer";

        byte[] inputBytes = Encoding.UTF8.GetBytes("OfflinePlayer:" + username.Trim());
        byte[] hash = MD5.HashData(inputBytes);

        // Set version to 3 (Name-based MD5)
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        // Set variant to IETF RFC 4122
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        var sb = new StringBuilder(36);
        for (int i = 0; i < 16; i++)
        {
            sb.Append(hash[i].ToString("x2"));
            if (dashed && (i == 3 || i == 5 || i == 7 || i == 9))
                sb.Append('-');
        }
        return sb.ToString();
    }

    public static string SanitizeUsername(string rawUsername)
    {
        if (string.IsNullOrWhiteSpace(rawUsername))
            return "ShinraiPlayer";

        // Minecraft usernames are 3-16 chars, alphanumeric + underscore
        string cleaned = Regex.Replace(rawUsername.Trim(), @"[^a-zA-Z0-9_]", "_");
        if (cleaned.Length < 3)
            cleaned = cleaned.PadRight(3, '_');
        if (cleaned.Length > 16)
            cleaned = cleaned[..16];

        return cleaned;
    }
}
