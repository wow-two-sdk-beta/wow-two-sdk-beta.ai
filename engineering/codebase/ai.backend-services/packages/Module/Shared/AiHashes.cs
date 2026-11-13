using System.Security.Cryptography;
using System.Text;
using WoW.Two.Sdk.Beta.Be.Ai.Prompts;

namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;

/// <summary>Computes the hashes the module stores: a prompt row's content hash and an attempt's input hash.</summary>
internal static class AiHashes
{
    /// <summary>Separates the parts a hash covers, so moving text from one part to the next changes the hash.</summary>
    private const char Separator = '\u001f';

    /// <summary>Gets a prompt row's content hash: the SHA-256 of its templates and canonical parameters, first 12 hex
    /// characters, lowercase.</summary>
    /// <param name="systemTemplate">The system template.</param>
    /// <param name="userTemplate">The user template.</param>
    /// <param name="parameters">The call parameters.</param>
    public static string Content(string systemTemplate, string userTemplate, PromptParameters parameters) =>
        Sha256(string.Join(Separator, systemTemplate, userTemplate, AiJson.CanonicalParameters(parameters)))[..12];

    /// <summary>Gets an attempt's input hash: the SHA-256 of the rendered messages, the schema, the model and the call
    /// parameters, lowercase hex.</summary>
    /// <param name="system">The rendered system message.</param>
    /// <param name="user">The rendered user message.</param>
    /// <param name="schema">The output schema, when the use case has one.</param>
    /// <param name="model">The model reference, <c>{provider}/{model}</c>.</param>
    /// <param name="parameters">The call parameters in effect.</param>
    public static string Input(string system, string user, string? schema, string model, PromptParameters parameters) =>
        Sha256(string.Join(Separator, system, user, schema ?? string.Empty, model, AiJson.CanonicalParameters(parameters)));

    /// <summary>Gets the SHA-256 of a text's UTF-8 bytes, lowercase hex.</summary>
    /// <param name="text">The text.</param>
    public static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
