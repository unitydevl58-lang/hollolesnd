using UnityEngine;

/// <summary>
/// Resolves semantic or HTML colors into Unity colors.
/// </summary>
public interface IColorResolver
{
    bool TryResolve(string rawColor, out Color color);
}
