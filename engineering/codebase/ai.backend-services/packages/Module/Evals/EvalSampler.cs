using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WoW.Two.Sdk.Beta.Be.Ai.Evals;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Evals;

/// <summary>Picks the items an eval run takes: an explicit subset, then the items carrying every tag, then a sample of
/// them ranked by a hash of the seed and each item's id.</summary>
/// <remarks>The hash ranks each item on its own, so the same seed keeps the same items however the set is listed, and an
/// item added later displaces one a rerun took only when it ranks ahead of it.</remarks>
internal static class EvalSampler
{
    /// <summary>Normalizes a request's sample fields: tags trimmed and deduplicated, ids deduplicated, the count checked.</summary>
    /// <param name="count">How many items to keep.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="tags">The tags an item must carry.</param>
    /// <param name="itemIds">The items to take.</param>
    /// <returns>The sample, or a validation error for a count below 1.</returns>
    public static Result<EvalSampleModel> Normalize(int? count, int? seed, IReadOnlyList<string>? tags, IReadOnlyList<Guid>? itemIds)
    {
        if (count is < 1)
            return Result<EvalSampleModel>.Fail(AiErrors.Invalid($"A sample takes 1 item or more, not {count}."));

        return Result<EvalSampleModel>.Ok(new EvalSampleModel
        {
            Count = count,
            Seed = seed,
            Tags = [.. (tags ?? []).Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.Ordinal)],
            ItemIds = [.. (itemIds ?? []).Where(id => id != Guid.Empty).Distinct()],
        });
    }

    /// <summary>Gets whether a sample takes the whole set.</summary>
    /// <param name="sample">The sample.</param>
    public static bool IsWholeSet(EvalSampleModel sample) =>
        sample.Count is null && sample.Tags.Count == 0 && sample.ItemIds.Count == 0;

    /// <summary>Picks the items <paramref name="sample"/> takes from <paramref name="items"/>, keeping their order.</summary>
    /// <param name="set">The set key, for the errors.</param>
    /// <param name="items">The set's active items, oldest first.</param>
    /// <param name="sample">The sample.</param>
    /// <returns>The items, or an error: an id that is no active item of the set, or filters that keep no item.</returns>
    public static Result<IReadOnlyList<EvalItemModel>> Select(string set, IReadOnlyList<EvalItemModel> items, EvalSampleModel sample)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(sample);
        IEnumerable<EvalItemModel> kept = items;
        if (sample.ItemIds.Count > 0)
        {
            var known = items.Select(item => item.Id).ToHashSet();
            if (sample.ItemIds.FirstOrDefault(id => !known.Contains(id)) is var missing && missing != Guid.Empty)
                return Result<IReadOnlyList<EvalItemModel>>.Fail(AiErrors.Invalid($"Eval item '{missing}' is not an item of set '{set}'."));
            var wanted = sample.ItemIds.ToHashSet();
            kept = kept.Where(item => wanted.Contains(item.Id));
        }

        if (sample.Tags.Count > 0)
            kept = kept.Where(item => sample.Tags.All(tag => item.Tags.Contains(tag, StringComparer.Ordinal)));

        var list = kept.ToList();
        if (list.Count == 0 && !IsWholeSet(sample))
            return Result<IReadOnlyList<EvalItemModel>>.Fail(AiErrors.Invalid($"The sample keeps no item of set '{set}'."));

        if (sample.Count is { } count && count < list.Count)
        {
            var seed = sample.Seed ?? AiConstants.DefaultSampleSeed;
            var chosen = list.OrderBy(item => Rank(seed, item.Id)).ThenBy(item => item.Id).Take(count).Select(item => item.Id).ToHashSet();
            list = [.. list.Where(item => chosen.Contains(item.Id))];
        }

        return Result<IReadOnlyList<EvalItemModel>>.Ok(list);
    }

    /// <summary>Ranks an item for a seed: the first 8 bytes of the SHA-256 of <c>{seed}:{id}</c>.</summary>
    /// <param name="seed">The seed.</param>
    /// <param name="id">The item id.</param>
    internal static ulong Rank(int seed, Guid id)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{seed}:{id:N}")), hash);
        return BinaryPrimitives.ReadUInt64BigEndian(hash);
    }
}
