// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.ResourceGroups;

/// <summary>
/// The directed dependency graph over Radius resource groups whose edges are the
/// <b>union</b> of cross-group <c>WithReference</c> relationships and cross-group
/// environment targets (FR-006, FR-009). An edge <c>from → to</c> means group
/// <c>from</c> depends on group <c>to</c> (so <c>to</c> is deployed first). Used to
/// detect cycles and to compute a deterministic deploy order.
/// </summary>
internal sealed class RadiusGroupGraph
{
    private readonly List<string> _nodes;
    private readonly Dictionary<string, HashSet<string>> _edges;

    private RadiusGroupGraph(List<string> nodes, Dictionary<string, HashSet<string>> edges)
    {
        _nodes = nodes;
        _edges = edges;
    }

    /// <summary>The graph's group nodes in declaration order.</summary>
    internal IReadOnlyList<string> Nodes => _nodes;

    /// <summary>
    /// Builds a graph from a declaration-ordered node list and a set of directed edges.
    /// Self-edges and edges referencing unknown nodes are ignored; duplicate edges collapse.
    /// </summary>
    internal static RadiusGroupGraph Build(
        IReadOnlyList<string> nodesInDeclarationOrder,
        IEnumerable<(string From, string To)> edges)
    {
        ArgumentNullException.ThrowIfNull(nodesInDeclarationOrder);
        ArgumentNullException.ThrowIfNull(edges);

        var nodes = new List<string>();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodesInDeclarationOrder)
        {
            if (known.Add(node))
            {
                nodes.Add(node);
            }
        }

        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            map[node] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var (from, to) in edges)
        {
            if (!string.Equals(from, to, StringComparison.Ordinal) &&
                known.Contains(from) && known.Contains(to))
            {
                map[from].Add(to);
            }
        }

        return new RadiusGroupGraph(nodes, map);
    }

    /// <summary>
    /// Returns a cycle (as the ordered list of groups forming it, with the entry group
    /// repeated at the end) if the graph contains one, otherwise <see langword="null"/>.
    /// </summary>
    internal IReadOnlyList<string>? FindCycle()
    {
        // 0 = unvisited, 1 = on the current DFS stack, 2 = fully explored.
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new List<string>();

        foreach (var node in _nodes)
        {
            var cycle = Visit(node, state, stack);
            if (cycle is not null)
            {
                return cycle;
            }
        }

        return null;
    }

    private IReadOnlyList<string>? Visit(string node, Dictionary<string, int> state, List<string> stack)
    {
        state.TryGetValue(node, out var s);
        if (s == 2)
        {
            return null;
        }

        if (s == 1)
        {
            // Back-edge into the current stack ⇒ cycle. Slice from the first occurrence.
            var index = stack.IndexOf(node);
            var cycle = stack.Skip(index).ToList();
            cycle.Add(node);
            return cycle;
        }

        state[node] = 1;
        stack.Add(node);

        foreach (var next in _edges[node].OrderBy(static x => x, StringComparer.Ordinal))
        {
            var cycle = Visit(next, state, stack);
            if (cycle is not null)
            {
                return cycle;
            }
        }

        stack.RemoveAt(stack.Count - 1);
        state[node] = 2;
        return null;
    }

    /// <summary>
    /// Returns the deploy order: every group appears after all the groups it depends on
    /// (SC-002). Ties are broken by declaration order for stable logs/tests. Assumes the
    /// graph is acyclic (validated via <see cref="FindCycle"/> first).
    /// </summary>
    internal IReadOnlyList<string> TopologicalOrder()
    {
        var order = new List<string>();
        var state = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var node in _nodes)
        {
            VisitForOrder(node, state, order);
        }

        return order;
    }

    private void VisitForOrder(string node, Dictionary<string, int> state, List<string> order)
    {
        state.TryGetValue(node, out var s);
        if (s != 0)
        {
            // Already fully explored, or currently on the stack (cycle guard — the graph
            // should be acyclic by the time ordering runs).
            return;
        }

        state[node] = 1;

        // Visit dependencies first, in declaration order, so they land earlier in the output.
        foreach (var dependency in _edges[node].OrderBy(DeclarationIndex))
        {
            VisitForOrder(dependency, state, order);
        }

        state[node] = 2;
        order.Add(node);
    }

    private int DeclarationIndex(string node) => _nodes.IndexOf(node);
}
