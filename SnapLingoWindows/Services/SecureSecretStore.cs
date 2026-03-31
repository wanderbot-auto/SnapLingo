using System.Security.Cryptography;
using System.Text;

namespace SnapLingoWindows.Services;

public sealed class SecureSecretStore
{
    public string LoadSecret(ProviderKind provider)
    {
        var path = GetPath(provider);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        var encrypted = File.ReadAllBytes(path);
        var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(decrypted);
    }

    public void SaveSecret(string secret, ProviderKind provider)
    {
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(GetPath(provider), encrypted);
    }

    public void DeleteSecret(ProviderKind provider)
    {
        var path = GetPath(provider);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string GetPath(ProviderKind provider)
    {
        return Path.Combine(AppPaths.SecretDirectory, provider.SecretFileName());
    }
}
