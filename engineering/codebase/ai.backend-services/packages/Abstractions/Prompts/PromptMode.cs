namespace WoW.Two.Sdk.Beta.Be.Ai.Prompts;

/// <summary>Refers to how a use case keeps its prompts; both modes share one table.</summary>
public enum PromptMode
{
    /// <summary>A save updates the prompt row in place; its version never moves. For products that need no prompt audit.</summary>
    Rewrite,

    /// <summary>A save inserts the next version and never updates a row; labels pick the version in use.</summary>
    Versioned,
}
