// File: GridAlgorithms.cs
// Purpose: Provide bounded A* pathfinding, line-of-sight, and influence-map calculations.
// Public API: PathResult, GridAlgorithms.FindPath, HasLineOfSight, and BuildInfluenceMap.
// Variables: Open-set heap, cost maps, and expansion counters remain method-local and bounded.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

namespace TacticalAI.Core;

/// <summary>Contains an A* result plus diagnostic budget information.</summary>
public sealed class PathResult
{
    public PathResult(IReadOnlyList<GridPosition> path, int totalCost, int nodesExpanded, bool budgetExhausted)
    {
        Path = path;
        TotalCost = totalCost;
        NodesExpanded = nodesExpanded;
        BudgetExhausted = budgetExhausted;
    }

    public IReadOnlyList<GridPosition> Path { get; }

    public int TotalCost { get; }

    public int NodesExpanded { get; }

    public bool BudgetExhausted { get; }
}

/// <summary>Implements deterministic grid algorithms without engine dependencies.</summary>
public static class GridAlgorithms
{
    /// <summary>Finds a lowest-cost cardinal path with an admissible Manhattan heuristic.</summary>
    public static PathResult FindPath(
        GridMap map,
        GridPosition start,
        GridPosition goal,
        Func<GridPosition, bool>? isBlocked = null,
        int expansionBudget = 4_096)
    {
        if (map is null)
        {
            throw new ArgumentNullException(nameof(map));
        }
        if (!map.Contains(start) || !map.Contains(goal) || expansionBudget < 1)
        {
            return new PathResult(Array.Empty<GridPosition>(), 0, 0, expansionBudget < 1);
        }

        if (start == goal)
        {
            return new PathResult(new[] { start }, 0, 0, false);
        }

        var open = new StableMinHeap();
        var cameFrom = new Dictionary<GridPosition, GridPosition>();
        var cost = new Dictionary<GridPosition, int> { [start] = 0 };
        open.Push(new HeapNode(start, start.ManhattanDistance(goal), 0));
        int expanded = 0;

        while (open.Count > 0)
        {
            HeapNode current = open.Pop();
            if (!cost.TryGetValue(current.Position, out int knownCost) || knownCost != current.Cost)
            {
                continue;
            }

            if (current.Position == goal)
            {
                return new PathResult(Reconstruct(cameFrom, goal), knownCost, expanded, false);
            }

            if (expanded >= expansionBudget)
            {
                return new PathResult(Array.Empty<GridPosition>(), 0, expanded, true);
            }

            expanded++;
            foreach (GridPosition neighbor in current.Position.CardinalNeighbors())
            {
                if (!map.Contains(neighbor) || !map[neighbor].IsWalkable || (neighbor != goal && isBlocked?.Invoke(neighbor) == true))
                {
                    continue;
                }

                int nextCost = checked(knownCost + map[neighbor].MoveCost);
                if (cost.TryGetValue(neighbor, out int oldCost) && oldCost <= nextCost)
                {
                    continue;
                }

                cost[neighbor] = nextCost;
                cameFrom[neighbor] = current.Position;
                open.Push(new HeapNode(neighbor, checked(nextCost + neighbor.ManhattanDistance(goal)), nextCost));
            }
        }

        return new PathResult(Array.Empty<GridPosition>(), 0, expanded, false);
    }

    /// <summary>Checks line of sight using a symmetric integer supercover traversal.</summary>
    public static bool HasLineOfSight(GridMap map, GridPosition from, GridPosition to)
    {
        if (map is null)
        {
            throw new ArgumentNullException(nameof(map));
        }
        if (!map.Contains(from) || !map.Contains(to))
        {
            return false;
        }

        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        int nx = Math.Abs(dx);
        int ny = Math.Abs(dy);
        int signX = Math.Sign(dx);
        int signY = Math.Sign(dy);
        int x = from.X;
        int y = from.Y;
        int ix = 0;
        int iy = 0;

        while (ix < nx || iy < ny)
        {
            long decision = ((1L + (2L * ix)) * ny) - ((1L + (2L * iy)) * nx);
            if (decision == 0)
            {
                x += signX;
                y += signY;
                ix++;
                iy++;
            }
            else if (decision < 0)
            {
                x += signX;
                ix++;
            }
            else
            {
                y += signY;
                iy++;
            }

            var position = new GridPosition(x, y);
            if (position != to && !map[position].IsWalkable)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Builds a distance-decayed threat field in O(cells multiplied by sources).</summary>
    public static int[,] BuildInfluenceMap(GridMap map, IEnumerable<GridPosition> sources, int radius, int peak = 100)
    {
        if (map is null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }
        if (radius < 1 || peak < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        GridPosition[] validSources = sources.Where(map.Contains).Distinct().OrderBy(position => position).ToArray();
        var field = new int[map.Width, map.Height];
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var position = new GridPosition(x, y);
                int strongest = 0;
                foreach (GridPosition source in validSources)
                {
                    int distance = position.ManhattanDistance(source);
                    if (distance <= radius)
                    {
                        strongest = Math.Max(strongest, peak - ((peak * distance) / (radius + 1)));
                    }
                }

                field[x, y] = strongest;
            }
        }

        return field;
    }

    private static IReadOnlyList<GridPosition> Reconstruct(
        IReadOnlyDictionary<GridPosition, GridPosition> cameFrom,
        GridPosition goal)
    {
        var path = new List<GridPosition> { goal };
        while (cameFrom.TryGetValue(path[^1], out GridPosition parent))
        {
            path.Add(parent);
        }

        path.Reverse();
        return path;
    }

    private readonly struct HeapNode
    {
        public HeapNode(GridPosition position, int priority, int cost)
        {
            Position = position;
            Priority = priority;
            Cost = cost;
        }

        public GridPosition Position { get; }

        public int Priority { get; }

        public int Cost { get; }
    }

    private sealed class StableMinHeap
    {
        private readonly List<HeapNode> items = new();

        public int Count => items.Count;

        public void Push(HeapNode item)
        {
            items.Add(item);
            int index = items.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (Compare(items[parent], items[index]) <= 0)
                {
                    break;
                }

                (items[parent], items[index]) = (items[index], items[parent]);
                index = parent;
            }
        }

        public HeapNode Pop()
        {
            HeapNode root = items[0];
            HeapNode last = items[^1];
            items.RemoveAt(items.Count - 1);
            if (items.Count == 0)
            {
                return root;
            }

            items[0] = last;
            int index = 0;
            while (true)
            {
                int left = (index * 2) + 1;
                if (left >= items.Count)
                {
                    break;
                }

                int right = left + 1;
                int child = right < items.Count && Compare(items[right], items[left]) < 0 ? right : left;
                if (Compare(items[index], items[child]) <= 0)
                {
                    break;
                }

                (items[index], items[child]) = (items[child], items[index]);
                index = child;
            }

            return root;
        }

        private static int Compare(HeapNode left, HeapNode right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            int cost = left.Cost.CompareTo(right.Cost);
            return cost != 0 ? cost : left.Position.CompareTo(right.Position);
        }
    }
}
