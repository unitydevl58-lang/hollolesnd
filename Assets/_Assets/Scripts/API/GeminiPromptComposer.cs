using System.Text;

/// <summary>
/// Builds the final user prompt with symbolic context detected locally.
/// </summary>
public sealed class GeminiPromptComposer
{
    public string Compose(string sanitizedPrompt, SymbolicAnalysisResult symbolicAnalysis)
    {
        StringBuilder builder = new StringBuilder(512);
        builder.AppendLine("User command:");
        builder.AppendLine(sanitizedPrompt ?? string.Empty);

        if (symbolicAnalysis != null)
        {
            builder.AppendLine();
            builder.AppendLine("Local symbolic deconstruction:");
            builder.Append("binaryPartitionRequested=");
            builder.Append(symbolicAnalysis.RequestsBinaryPartition ? "true" : "false");
            builder.Append("; preferredAxis=");
            builder.AppendLine(symbolicAnalysis.PreferredPartitionAxis.ToString().ToLowerInvariant());

            builder.Append("symbols=");
            bool wroteAnySymbol = false;
            for (int index = 0; index < symbolicAnalysis.Symbols.Count; index++)
            {
                CommandSymbol symbol = symbolicAnalysis.Symbols[index];
                if (symbol.Type == CommandSymbolType.Unknown)
                    continue;

                if (wroteAnySymbol)
                    builder.Append(", ");

                builder.Append(symbol.Type);
                builder.Append(":");
                builder.Append(symbol.NormalizedText);
                wroteAnySymbol = true;
            }

            if (!wroteAnySymbol)
                builder.Append("none");

            builder.AppendLine();
        }

        return builder.ToString();
    }
}
