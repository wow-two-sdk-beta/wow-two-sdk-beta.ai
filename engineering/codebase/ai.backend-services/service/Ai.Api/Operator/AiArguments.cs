using System.Globalization;

namespace Ai.Api.Operator;

/// <summary>Holds one <c>ai</c> verb's parsed arguments: the command words (<c>eval run</c>), each option's values and the
/// flags.</summary>
/// <param name="Command">The command, its words joined by spaces.</param>
/// <param name="Options">Each option's values, in order; a repeatable option may carry several.</param>
/// <param name="Flags">The flags given.</param>
public sealed record AiArguments(string Command, IReadOnlyDictionary<string, IReadOnlyList<string>> Options, IReadOnlySet<string> Flags)
{
    /// <summary>Gets every command with its options: required ones bare, optional ones in brackets, flags as <c>--name</c>.</summary>
    public static IReadOnlyDictionary<string, AiCommandSpec> Specs { get; } = new Dictionary<string, AiCommandSpec>(StringComparer.Ordinal)
    {
        ["usecase put"] = new(["key", "mode", "route"], ["schema-file", "description", "keep-outputs"]),
        ["usecase list"] = new([], []),
        ["prompt save"] = new(["use-case", "system-file", "user-file"], ["model-match", "temperature", "max-tokens", "effort", "note", "by"]),
        ["prompt list"] = new(["use-case"], []),
        ["label move"] = new(["use-case", "label", "version"], ["by"]),
        ["price put"] = new(["provider", "model", "input", "output"], ["effective-from", "cache-write", "cache-read", "source"]),
        ["price list"] = new([], []),
        ["run"] = new(["use-case"], ["var", "var-file", "model", "version", "label", "subject"], Flags: ["fresh"], Repeatable: ["var", "var-file", "model"]),
        ["inferences"] = new([], ["use-case", "subject", "run", "limit"]),
        ["totals"] = new(["from", "to"], []),
        ["eval set put"] = new(["key", "use-case"], ["score-fields", "severe", "note"]),
        ["eval sets"] = new([], []),
        ["eval add"] = new(["set", "expected"], ["subject", "input-file", "tags", "by"]),
        ["eval relabel"] = new(["item", "expected"], ["by"]),
        ["eval remove"] = new(["item"], []),
        ["eval items"] = new(["set"], []),
        ["eval run"] = new(["set", "model"], ["version", "label", "parallel", "note"], Flags: ["fresh"]),
        ["eval runs"] = new(["set"], []),
        ["eval report"] = new(["set"], ["run", "format"], Flags: ["answers"]),
        ["eval rate"] = new(["run", "item", "rating"], ["comment", "by"]),
        ["eval export"] = new(["set", "out"], []),
        ["eval import"] = new(["file"], []),
    };

    /// <summary>Parses the arguments after <c>ai</c>.</summary>
    /// <param name="args">The arguments, such as <c>eval run --set afisha-10 --model anthropic/claude-haiku-5-5</c>.</param>
    /// <param name="arguments">The parsed arguments.</param>
    /// <param name="problem">Why they do not parse, when they do not: the command is unknown, an option is unknown or
    /// repeated, a value is missing, or a required option is absent.</param>
    /// <returns>Whether they parse.</returns>
    public static bool TryParse(IReadOnlyList<string> args, out AiArguments arguments, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(args);
        arguments = new AiArguments(string.Empty, new Dictionary<string, IReadOnlyList<string>>(), new HashSet<string>());
        var words = args.TakeWhile(arg => !arg.StartsWith("--", StringComparison.Ordinal)).ToList();
        var command = string.Join(' ', words);
        if (!Specs.TryGetValue(command, out var spec))
        {
            problem = words.Count == 0 ? "name a command" : $"'{command}' is not a command";
            return false;
        }

        var options = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var index = words.Count; index < args.Count; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal))
            {
                problem = $"'{args[index]}' is not an option";
                return false;
            }

            var name = args[index][2..];
            if (spec.Flags.Contains(name))
            {
                flags.Add(name);
                continue;
            }

            if (!spec.Required.Contains(name) && !spec.Optional.Contains(name))
            {
                problem = $"'--{name}' is not an option of '{command}'";
                return false;
            }

            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                problem = $"'--{name}' needs a value";
                return false;
            }

            if (options.TryGetValue(name, out var values) && !spec.Repeatable.Contains(name))
            {
                problem = $"'--{name}' is given twice";
                return false;
            }

            (options.TryGetValue(name, out values) ? values : options[name] = []).Add(args[++index]);
        }

        if (spec.Required.FirstOrDefault(name => !options.ContainsKey(name)) is { } absent)
        {
            problem = $"'--{absent}' is required";
            return false;
        }

        problem = null;
        arguments = new AiArguments(command, options.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.Ordinal), flags);
        return true;
    }

    /// <summary>Gets an option's single value, or none.</summary>
    /// <param name="name">The option, without its dashes.</param>
    public string? Value(string name) => Options.TryGetValue(name, out var values) ? values[^1] : null;

    /// <summary>Gets every value of a repeatable option, each split on commas.</summary>
    /// <param name="name">The option, without its dashes.</param>
    public IReadOnlyList<string> List(string name) =>
        Options.TryGetValue(name, out var values)
            ? [.. values.SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))]
            : [];

    /// <summary>Gets a repeatable option's raw values, unsplit.</summary>
    /// <param name="name">The option, without its dashes.</param>
    public IReadOnlyList<string> All(string name) => Options.TryGetValue(name, out var values) ? values : [];

    /// <summary>Gets whether a flag was given.</summary>
    /// <param name="name">The flag, without its dashes.</param>
    public bool Flag(string name) => Flags.Contains(name);

    /// <summary>Reads an integer option; none when absent.</summary>
    /// <param name="name">The option.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the option is absent or an integer.</returns>
    public bool TryInt(string name, out int? value) => TryParse(name, out value, text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null);

    /// <summary>Reads a long option; none when absent.</summary>
    /// <param name="name">The option.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the option is absent or an integer.</returns>
    public bool TryLong(string name, out long? value) => TryParse(name, out value, text => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null);

    /// <summary>Reads a number option; none when absent.</summary>
    /// <param name="name">The option.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the option is absent or a number.</returns>
    public bool TryDouble(string name, out double? value) => TryParse(name, out value, text => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null);

    /// <summary>Reads an instant option (a date reads as its UTC midnight); none when absent.</summary>
    /// <param name="name">The option.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the option is absent or an instant.</returns>
    public bool TryInstant(string name, out DateTimeOffset? value) => TryParse(name, out value, text =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) ? parsed : null);

    /// <summary>Reads a Guid option; none when absent.</summary>
    /// <param name="name">The option.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the option is absent or a Guid.</returns>
    public bool TryGuid(string name, out Guid? value) => TryParse(name, out value, text => Guid.TryParse(text, out var parsed) ? parsed : null);

    /// <summary>Reads a true/false option; none when absent.</summary>
    /// <param name="name">The option.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the option is absent or a boolean.</returns>
    public bool TryBool(string name, out bool? value) => TryParse(name, out value, text => bool.TryParse(text, out var parsed) ? parsed : null);

    private bool TryParse<T>(string name, out T? value, Func<string, T?> parse) where T : struct
    {
        value = null;
        if (Value(name) is not { } text)
            return true;

        value = parse(text.Trim());
        return value is not null;
    }
}

/// <summary>Describes one command's options.</summary>
/// <param name="Required">The options it needs.</param>
/// <param name="Optional">The options it takes.</param>
/// <param name="Flags">The flags it takes.</param>
/// <param name="Repeatable">The options it takes more than once.</param>
public sealed record AiCommandSpec(IReadOnlyList<string> Required, IReadOnlyList<string> Optional, IReadOnlyList<string>? Flags = null, IReadOnlyList<string>? Repeatable = null)
{
    /// <summary>Gets the flags it takes.</summary>
    public IReadOnlyList<string> Flags { get; init; } = Flags ?? [];

    /// <summary>Gets the options it takes more than once.</summary>
    public IReadOnlyList<string> Repeatable { get; init; } = Repeatable ?? [];

    /// <summary>Writes the command's usage line.</summary>
    /// <param name="command">The command.</param>
    public string Usage(string command) => string.Join(' ', new[] { "ai", command }
        .Concat(Required.Select(name => $"--{name} <{name}>"))
        .Concat(Optional.Select(name => Repeatable.Contains(name) ? $"[--{name} <{name}>]…" : $"[--{name} <{name}>]"))
        .Concat(Flags.Select(name => $"[--{name}]")));
}
