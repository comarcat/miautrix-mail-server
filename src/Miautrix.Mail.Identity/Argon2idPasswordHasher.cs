using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Miautrix.Mail.Identity;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hashedPassword);
}

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    public sealed record Argon2idHasherOptions(
        int MemoryKb,
        int Iterations,
        int Parallelism);

    public static Argon2idHasherOptions CurrentOptions =>
        new(MemorySize, Iterations, DegreeOfParallelism);

    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 3;
    private const int MemorySize = 65536; // 64 MB
    private const int DegreeOfParallelism = 2;

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = HashInternal(password, salt, Iterations, MemorySize, DegreeOfParallelism);

        return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={DegreeOfParallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hashedPassword))
        {
            return false;
        }

        var parts = hashedPassword.Split('$');
        // Format: ["", "argon2id", "v=19", "m=65536,t=3,p=2", "<salt>", "<hash>"]
        if (parts.Length != 6 || parts[1] != "argon2id")
        {
            return false;
        }

        try
        {
            int iterations = Iterations;
            int memorySize = MemorySize;
            int parallelism = DegreeOfParallelism;

            var paramsTokens = parts[3].Split(',');
            foreach (var token in paramsTokens)
            {
                var kv = token.Split('=');
                if (kv.Length == 2)
                {
                    if (kv[0] == "m" && int.TryParse(kv[1], out var m)) memorySize = m;
                    else if (kv[0] == "t" && int.TryParse(kv[1], out var t)) iterations = t;
                    else if (kv[0] == "p" && int.TryParse(kv[1], out var p)) parallelism = p;
                }
            }

            byte[] salt = Convert.FromBase64String(parts[4]);
            byte[] expectedHash = Convert.FromBase64String(parts[5]);

            byte[] computedHash = HashInternal(password, salt, iterations, memorySize, parallelism);
            return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] HashInternal(string password, byte[] salt, int iterations, int memorySize, int parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            MemorySize = memorySize,
            Iterations = iterations
        };

        return argon2.GetBytes(HashSize);
    }
}
