using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Persistence;

/// <summary>The Ai module's context — a pure mapper over the SQL-owned schema in <c>Migrations/</c>, on snake_case columns.</summary>
/// <remarks>
///   - internal: consumers reach the store through the Ai brokers and the runner only, never its tables
///   - enumerations are text holding the contract's members in snake_case (<c>AiLabels</c>); structured values are jsonb
///     mapped as strings; lists are text[]
///   - the report reads the <c>ai_eval_report_rows</c> view as a keyless entity
/// </remarks>
/// <param name="options">The context options, configured by <c>AddAi</c>.</param>
internal sealed class AiDbContext(DbContextOptions<AiDbContext> options) : AppDbContextBase(options)
{
    public DbSet<UseCaseEntity> UseCases => Set<UseCaseEntity>();
    public DbSet<PromptEntity> Prompts => Set<PromptEntity>();
    public DbSet<PromptLabelEntity> PromptLabels => Set<PromptLabelEntity>();
    public DbSet<PromptLabelMoveEntity> PromptLabelMoves => Set<PromptLabelMoveEntity>();
    public DbSet<ModelPriceEntity> ModelPrices => Set<ModelPriceEntity>();
    public DbSet<InferenceEntity> Inferences => Set<InferenceEntity>();
    public DbSet<EvalSetEntity> EvalSets => Set<EvalSetEntity>();
    public DbSet<EvalItemEntity> EvalItems => Set<EvalItemEntity>();
    public DbSet<EvalRunEntity> EvalRuns => Set<EvalRunEntity>();
    public DbSet<EvalScoreEntity> EvalScores => Set<EvalScoreEntity>();
    public DbSet<EvalReportRow> EvalReportRows => Set<EvalReportRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AiModuleConstants.SchemaName);

        var useCase = modelBuilder.Entity<UseCaseEntity>();
        useCase.ToTable("ai_use_cases");
        useCase.HasKey(row => row.Key);
        useCase.Property(row => row.Key).HasMaxLength(AiConstants.MaxKeyLength);
        useCase.Property(row => row.PromptMode).HasMaxLength(16);
        useCase.Property(row => row.OutputSchema).HasColumnType("jsonb");

        var prompt = modelBuilder.Entity<PromptEntity>();
        prompt.ToTable("ai_prompts");
        prompt.HasKey(row => row.Id);
        prompt.Property(row => row.UseCase).HasMaxLength(AiConstants.MaxKeyLength);
        prompt.Property(row => row.ModelMatch).HasMaxLength(AiConstants.MaxModelRefLength);
        prompt.Property(row => row.Parameters).HasColumnType("jsonb");
        prompt.Property(row => row.ContentHash).HasMaxLength(12);
        prompt.Property(row => row.CreatedBy).HasMaxLength(256);

        var label = modelBuilder.Entity<PromptLabelEntity>();
        label.ToTable("ai_prompt_labels");
        label.HasKey(row => new { row.UseCase, row.Label });
        label.Property(row => row.UseCase).HasMaxLength(AiConstants.MaxKeyLength);
        label.Property(row => row.Label).HasMaxLength(AiConstants.MaxKeyLength);
        label.Property(row => row.MovedBy).HasMaxLength(256);

        var move = modelBuilder.Entity<PromptLabelMoveEntity>();
        move.ToTable("ai_prompt_label_moves");
        move.HasKey(row => row.Id);
        move.Property(row => row.UseCase).HasMaxLength(AiConstants.MaxKeyLength);
        move.Property(row => row.Label).HasMaxLength(AiConstants.MaxKeyLength);
        move.Property(row => row.MovedBy).HasMaxLength(256);

        var price = modelBuilder.Entity<ModelPriceEntity>();
        price.ToTable("ai_model_prices");
        price.HasKey(row => new { row.Provider, row.Model, row.EffectiveFrom });
        price.Property(row => row.Provider).HasMaxLength(64);
        price.Property(row => row.Model).HasMaxLength(AiConstants.MaxModelRefLength);

        var inference = modelBuilder.Entity<InferenceEntity>();
        inference.ToTable("ai_inferences");
        inference.HasKey(row => row.Id);
        inference.Property(row => row.UseCase).HasMaxLength(AiConstants.MaxKeyLength);
        inference.Property(row => row.PromptHash).HasMaxLength(12);
        inference.Property(row => row.PromptModelMatch).HasMaxLength(AiConstants.MaxModelRefLength);
        inference.Property(row => row.Provider).HasMaxLength(64);
        inference.Property(row => row.Model).HasMaxLength(AiConstants.MaxModelRefLength);
        inference.Property(row => row.Subject).HasMaxLength(AiConstants.MaxSubjectLength);
        inference.Property(row => row.InputHash).HasMaxLength(64);
        inference.Property(row => row.Outcome).HasMaxLength(16);
        inference.Property(row => row.ErrorKind).HasMaxLength(32);
        inference.Property(row => row.FinishReason).HasMaxLength(64);

        var set = modelBuilder.Entity<EvalSetEntity>();
        set.ToTable("ai_eval_sets");
        set.HasKey(row => row.Key);
        set.Property(row => row.Key).HasMaxLength(AiConstants.MaxKeyLength);
        set.Property(row => row.UseCase).HasMaxLength(AiConstants.MaxKeyLength);

        var item = modelBuilder.Entity<EvalItemEntity>();
        item.ToTable("ai_eval_items");
        item.HasKey(row => row.Id);
        item.Property(row => row.SetKey).HasMaxLength(AiConstants.MaxKeyLength);
        item.Property(row => row.Subject).HasMaxLength(AiConstants.MaxSubjectLength);
        item.Property(row => row.Input).HasColumnType("jsonb");
        item.Property(row => row.Expected).HasColumnType("jsonb");
        item.Property(row => row.LabelledBy).HasMaxLength(256);

        var run = modelBuilder.Entity<EvalRunEntity>();
        run.ToTable("ai_eval_runs");
        run.HasKey(row => row.Id);
        run.Property(row => row.SetKey).HasMaxLength(AiConstants.MaxKeyLength);
        run.Property(row => row.UseCase).HasMaxLength(AiConstants.MaxKeyLength);
        run.Property(row => row.PromptHash).HasMaxLength(12);
        run.Property(row => row.Provider).HasMaxLength(64);
        run.Property(row => row.Model).HasMaxLength(AiConstants.MaxModelRefLength);

        var score = modelBuilder.Entity<EvalScoreEntity>();
        score.ToTable("ai_eval_scores");
        score.HasKey(row => new { row.RunId, row.ItemId });
        score.Property(row => row.Outcome).HasMaxLength(16);
        score.Property(row => row.Answer).HasColumnType("jsonb");
        score.Property(row => row.RatedBy).HasMaxLength(256);

        var report = modelBuilder.Entity<EvalReportRow>();
        report.ToView("ai_eval_report_rows");
        report.HasNoKey();
        report.Property(row => row.Expected).HasColumnType("jsonb");
        report.Property(row => row.Answer).HasColumnType("jsonb");

        base.OnModelCreating(modelBuilder);
    }
}
