using Ai.Api.Configurations;
using WoW.Two.Sdk.Beta.Be.Meta;

// Answer a verb, or build, configure and serve the host. The `ai` operator verbs (use cases, prompts, labels, prices,
// runs, the inference log and evals) run before any host boots, over the configuration and the database only; `migrate`
// and `config check` are the be sdk's own.
return await ServiceHost.RunAsync(
    args,
    builder => builder.Configure(),
    app => app.ConfigureAsync(),
    verbs => verbs.AddAiVerbs());

public partial class Program;
