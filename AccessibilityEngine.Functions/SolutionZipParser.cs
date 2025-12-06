using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AccessibilityEngine.Core.Models;

namespace AccessibilityEngine.Functions;

/// <summary>
/// Local fallback SolutionZipParser for Functions project.
/// Returns no UiTrees; adapters project should provide a richer implementation.
/// This keeps the Functions project buildable when the Adapters project is not loaded.
/// </summary>
public sealed class SolutionZipParser
{
    private readonly Func<byte[], Task<IReadOnlyList<UiTree>>> _parserDelegate;

    public SolutionZipParser()
    {
        // Try to locate an adapter implementation at runtime (optional project)
        try
        {
            var adapterType = Type.GetType("AccessibilityEngine.Adapters.PowerApps.SolutionZipParser, AccessibilityEngine.Adapters.PowerApps");
            if (adapterType != null)
            {
                var instance = Activator.CreateInstance(adapterType);
                var method = adapterType.GetMethod("ParseSolutionAsync", new[] { typeof(byte[]) });
                if (method != null && instance != null)
                {
                    _parserDelegate = async bytes =>
                    {
                        // Invoke the adapter method and await the returned task dynamically
                        dynamic task = method.Invoke(instance, new object[] { bytes })!;
                        var result = await task;
                        return (IReadOnlyList<UiTree>)result!;
                    };
                    return;
                }
            }
        }
        catch
        {
            // Ignore and fall back to empty parser
        }

        // Fallback: return no UiTrees
        _parserDelegate = bytes => Task.FromResult((IReadOnlyList<UiTree>)Array.Empty<UiTree>());
    }

    public Task<IReadOnlyList<UiTree>> ParseSolutionAsync(byte[] solutionZip)
    {
        if (solutionZip == null) throw new ArgumentNullException(nameof(solutionZip));
        return _parserDelegate(solutionZip);
    }
}
