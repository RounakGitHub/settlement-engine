using System.Security.Cryptography;
using System.Text;

namespace Splitr.Application.Helpers;

public static class TokenHasher
{
    public static string Hash(string token) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
