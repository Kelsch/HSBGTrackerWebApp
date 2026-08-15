using System.Security.Cryptography;
using System.Text;

namespace HSBGTrackerWebApp.Api.Auth;

public static class ApiKeyGenerator
{
    /// <summary>A new random API key, given to a friend exactly once when their account is created.</summary>
    public static string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>What actually gets stored - never the raw key itself.</summary>
    public static byte[] Hash(string apiKey) => SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
}
