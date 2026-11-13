# WoW2.Sdk.Beta.Be.Ai.Providers

Named model providers on the Microsoft.Extensions.AI `IChatClient` seam, and a one-attempt caller over them.

- **Kinds:** `anthropic` (the official `Anthropic` SDK), `openai` (any OpenAI-compatible Chat Completions host:
  OpenAI, Groq, DeepSeek, Ollama's `/v1`) and `ollama` (OllamaSharp, Ollama's native API).
- **`IAiModelCaller`** calls a model once and never throws for a provider failure: it returns the text as given, or a
  classified `AiErrorKind`, with token usage, latency and the provider's finish reason either way. Retries, routing,
  prices and the inference log belong to the Ai module; `AiRetry.IsTransient(kind)` says which failures to retry.
- **`IAiChatClients`** hands out one chat client per provider and model, wrapped with OpenTelemetry (`gen_ai` spans and
  metrics on the source `Experimental.Microsoft.Extensions.AI`).

```bash
dotnet add package WoW2.Sdk.Beta.Be.Ai.Providers --prerelease
```

## Configuration

Every child of `Ai:Providers` is one provider, named by its key. Keep keys in user secrets or the environment
(`Ai__Providers__anthropic__ApiKey`), never in a committed file.

```json
{
  "Ai": {
    "Providers": {
      "anthropic": { "ApiKey": "<from secrets>" },
      "groq": { "Kind": "openai", "BaseUrl": "https://api.groq.com/openai/v1", "ApiKey": "<from secrets>", "TimeoutSeconds": 30 },
      "ollama": { "BaseUrl": "http://localhost:11434", "TimeoutSeconds": 120 }
    }
  }
}
```

| Key | Meaning |
|---|---|
| `Kind` | `anthropic`, `openai` or `ollama`; empty takes the provider's name when it is one of these |
| `BaseUrl` | the host; empty takes the kind's public endpoint (`https://api.anthropic.com`, `https://api.openai.com/v1`, `http://localhost:11434`) and never an SDK environment variable |
| `ApiKey` | required for every kind but `ollama`; a provider without one stays unconfigured and its calls answer `NotConfigured` without a request. Ollama's `/v1` through `openai` takes any non-empty key |
| `TimeoutSeconds` | the per-attempt timeout (default 60); passing it answers `Timeout` |

A model is referenced as `{provider}/{model}`: `anthropic/claude-haiku-5-5`, `groq/openai/gpt-oss-20b`,
`ollama/llama3.2` (`AiModelRef.TryParse`).

## Usage

```csharp
services.AddAiProviders(configuration, providers =>
    providers.Providers["anthropic"].TimeoutSeconds = 30);   // optional: code runs after the binding

var result = await caller.CallAsync(new AiCallRequest
{
    Model = new AiModelRef("anthropic", "claude-haiku-5-5"),
    System = "You sort notes into one label.",
    User = "Sort: buy milk",
    JsonSchema = """{"type":"object","properties":{"label":{"type":"string"}},"required":["label"]}""",
    SchemaName = "sort-note",
    Effort = "none",
});
// result.Text, or result.ErrorKind + result.Error; result.InputTokens, CacheReadTokens, CacheWriteTokens, LatencyMs
```

A product that only wants a chat client registers one:

```csharp
services.AddAiProviders(configuration).AddAiChatClient("ollama", "llama3.2");
// inject IChatClient
```

## Behaviour

- **Request:** a system message (skipped when empty) and a user message; `ModelId`, `MaxOutputTokens`, `Temperature`;
  `JsonSchema` as a JSON-schema response format (Anthropic `output_config.format`, OpenAI `response_format`, Ollama
  `format`); `Effort` `none|low|medium|high` as `ChatOptions.Reasoning` (Anthropic: `none` disables thinking, the rest
  turn on adaptive thinking at that effort; OpenAI: `reasoning_effort`; Ollama: `think`). Leave `Effort` empty for a
  model without reasoning. `CacheSystem` marks Anthropic's system block `cache_control: ephemeral`.
- **Usage:** `InputTokens` holds every input token, cache reads and writes included, for every kind; `OutputTokens`
  includes reasoning. Ollama reports neither cache nor reasoning counts.
- **Errors:** 401/403 `Unauthorized`, 408 and the provider timeout `Timeout`, 429 `RateLimited`, 5xx and Anthropic's
  529 `Unavailable`, other 4xx `BadRequest`, unreachable hosts `Network`, a refusal (`refusal`, `content_filter`)
  `ContentFiltered` with no text, an empty answer `InvalidResponse`. `Error` holds the provider's message, at most 500
  characters, with the key removed. Every SDK runs a single attempt; the caller's own cancellation throws.
