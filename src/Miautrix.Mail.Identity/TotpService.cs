using OtpNet;

namespace Miautrix.Mail.Identity;

public interface ITotpService
{
    string GenerateSecret();
    string GenerateQrCodeUri(string secret, string accountName, string issuer = "Miautrix Mail");
    bool VerifyCode(string secret, string code);
}

public sealed class TotpService : ITotpService
{
    public string GenerateSecret()
    {
        byte[] secret = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(secret);
    }

    public string GenerateQrCodeUri(string secret, string accountName, string issuer = "Miautrix Mail")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);

        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountName);

        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        try
        {
            byte[] secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }
}
