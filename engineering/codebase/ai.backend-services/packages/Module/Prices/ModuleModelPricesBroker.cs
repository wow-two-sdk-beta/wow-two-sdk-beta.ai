using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Ai.Prices;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Prices;

/// <summary>Integrates model prices in process: rows with history, one per provider, model and start.</summary>
/// <param name="db">The module's store.</param>
internal sealed class ModuleModelPricesBroker(AiDbContext db) : IModelPricesBroker
{
    /// <inheritdoc />
    public async Task<Result<ModelPriceModel>> PutAsync(ModelPriceModel price, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(price);
        var provider = price.Provider?.Trim().ToLowerInvariant() ?? string.Empty;
        var model = price.Model?.Trim() ?? string.Empty;
        if (provider.Length is 0 or > 64 || model.Length is 0 or > AiConstants.MaxModelRefLength)
            return Result<ModelPriceModel>.Fail(AiErrors.Invalid("A price names its provider (at most 64 characters) and model."));
        if (price.InputPerMTokMicroUsd < 0 || price.OutputPerMTokMicroUsd < 0 || price.CacheReadPerMTokMicroUsd < 0 || price.CacheWritePerMTokMicroUsd < 0)
            return Result<ModelPriceModel>.Fail(AiErrors.Invalid("A price is never negative."));

        var from = StoredClock.Truncate(price.EffectiveFromUtc);
        var row = await db.ModelPrices.FirstOrDefaultAsync(
            entity => entity.Provider == provider && entity.Model == model && entity.EffectiveFrom == from, cancellationToken);
        if (row is null)
        {
            row = new ModelPriceEntity { Provider = provider, Model = model, EffectiveFrom = from };
            db.ModelPrices.Add(row);
        }

        row.InputPerMtokMicroUsd = price.InputPerMTokMicroUsd;
        row.OutputPerMtokMicroUsd = price.OutputPerMTokMicroUsd;
        row.CacheWritePerMtokMicroUsd = price.CacheWritePerMTokMicroUsd;
        row.CacheReadPerMtokMicroUsd = price.CacheReadPerMTokMicroUsd;
        row.Source = string.IsNullOrWhiteSpace(price.Source) ? null : price.Source.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return Result<ModelPriceModel>.Ok(AiModels.ToModel(row));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ModelPriceModel>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.ModelPrices.AsNoTracking()
            .OrderBy(entity => entity.Provider)
            .ThenBy(entity => entity.Model)
            .ThenByDescending(entity => entity.EffectiveFrom)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<ModelPriceModel>>.Ok([.. rows.Select(AiModels.ToModel)]);
    }
}
