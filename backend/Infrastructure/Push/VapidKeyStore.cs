using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WebPush;

namespace Mems.Infrastructure.Push;

/// <summary>The VAPID identity this server signs Web Push messages with.</summary>
public sealed record VapidKeys(string Subject, string PublicKey, string PrivateKey);

/// <summary>
/// Loads the VAPID key pair: config first (Push:PublicKey/PrivateKey via user-secrets/env for
/// real deployments), else a pair generated on first start and persisted to
/// App_Data/vapid-keys.json (git-ignored) so demo subscriptions survive restarts.
/// The private key never appears in source or committed config (CLAUDE.md §1.2).
/// </summary>
public static class VapidKeyStore
{
    public static VapidKeys LoadOrCreate(string dataDir, IConfiguration config)
    {
        var subject = config["Push:Subject"];
        if (string.IsNullOrWhiteSpace(subject)) subject = "mailto:mems@waves.com.pk";

        var configuredPublic = config["Push:PublicKey"];
        var configuredPrivate = config["Push:PrivateKey"];
        if (!string.IsNullOrWhiteSpace(configuredPublic) && !string.IsNullOrWhiteSpace(configuredPrivate))
            return new VapidKeys(subject, configuredPublic, configuredPrivate);

        var path = Path.Combine(dataDir, "vapid-keys.json");
        if (File.Exists(path))
        {
            var stored = JsonSerializer.Deserialize<StoredKeys>(File.ReadAllText(path));
            if (stored is { PublicKey.Length: > 0, PrivateKey.Length: > 0 })
                return new VapidKeys(subject, stored.PublicKey, stored.PrivateKey);
        }

        var generated = VapidHelper.GenerateVapidKeys();
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(path, JsonSerializer.Serialize(new StoredKeys(generated.PublicKey, generated.PrivateKey)));
        return new VapidKeys(subject, generated.PublicKey, generated.PrivateKey);
    }

    private sealed record StoredKeys(string PublicKey, string PrivateKey);
}
