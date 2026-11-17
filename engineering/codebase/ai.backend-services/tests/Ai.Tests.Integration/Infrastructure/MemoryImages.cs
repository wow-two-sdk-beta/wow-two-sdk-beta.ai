using System.Collections.Concurrent;
using System.Security.Cryptography;
using WoW.Two.Sdk.Beta.Be.Ai.Runs;

namespace Ai.Tests.Integration.Infrastructure;

/// <summary>A host's image resolver over an in-memory store: <c>fss:{name}</c> keys, as SCFD's file storage would answer
/// them; every key asked is recorded.</summary>
public sealed class MemoryImages : IAiImageResolver
{
    private readonly ConcurrentDictionary<string, byte[]> images = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> asked = new();

    /// <summary>Gets every key asked, oldest first.</summary>
    public IReadOnlyList<string> Asked => [.. asked];

    /// <summary>Stores a PNG under <c>fss:{name}</c>, its bytes made distinct by <paramref name="seed"/>.</summary>
    /// <param name="name">The key's name.</param>
    /// <param name="seed">A byte that tells two images apart.</param>
    /// <returns>The bytes stored.</returns>
    public byte[] Put(string name, byte seed = 1)
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, seed, seed, seed];
        images["fss:" + name] = png;
        return png;
    }

    /// <summary>Stores bytes as they are under <c>fss:{name}</c>.</summary>
    /// <param name="name">The key's name.</param>
    /// <param name="data">The bytes.</param>
    public void Put(string name, byte[] data) => images["fss:" + name] = data;

    /// <summary>Removes the image under <c>fss:{name}</c>.</summary>
    /// <param name="name">The key's name.</param>
    public void Remove(string name) => images.TryRemove("fss:" + name, out _);

    /// <summary>Gets a key's reference.</summary>
    /// <param name="name">The key's name.</param>
    public static AiImageRef Ref(string name) => new() { Key = "fss:" + name };

    /// <summary>Gets the SHA-256 of bytes, lowercase hex, as the module hashes them.</summary>
    /// <param name="data">The bytes.</param>
    public static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    /// <inheritdoc />
    public Task<AiImageContent?> ResolveAsync(AiImageRef image, CancellationToken cancellationToken = default)
    {
        if (image.Key is not { } key || !key.StartsWith("fss:", StringComparison.Ordinal))
            return Task.FromResult<AiImageContent?>(null);

        asked.Enqueue(key);
        return Task.FromResult(images.TryGetValue(key, out var data) ? new AiImageContent(data, "image/png") : null);
    }
}
