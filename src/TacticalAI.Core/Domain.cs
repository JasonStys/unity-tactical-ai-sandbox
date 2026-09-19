// File: Domain.cs
// Purpose: Define deterministic, engine-agnostic tactical state, commands, and results.
// Public API: GridPosition, TerrainCell, GridMap, UnitState, AbilityDefinition,
//             TacticalState, ActionCommand, ActionEvent, and ActionResult.
// Variables: Immutable values validate constructor input; mutable state is cloned at boundaries.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

namespace TacticalAI.Core;

/// <summary>Identifies the two opposing sides in a simulation.</summary>
public enum Team
{
    Blue = 0,
    Red = 1,
}

/// <summary>Identifies a command category in the deterministic turn log.</summary>
public enum ActionKind
{
    Move = 0,
    UseAbility = 1,
    Wait = 2,
}

/// <summary>Represents an integer grid coordinate with deterministic ordering.</summary>
public readonly struct GridPosition : IEquatable<GridPosition>, IComparable<GridPosition>
{
    public GridPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }

    /// <summary>Returns the Manhattan distance to another coordinate in O(1).</summary>
    public int ManhattanDistance(GridPosition other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    /// <summary>Returns cardinal neighbors in a stable top, right, bottom, left order.</summary>
    public IEnumerable<GridPosition> CardinalNeighbors()
    {
        yield return new GridPosition(X, Y + 1);
        yield return new GridPosition(X + 1, Y);
        yield return new GridPosition(X, Y - 1);
        yield return new GridPosition(X - 1, Y);
    }

    public bool Equals(GridPosition other) => X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is GridPosition other && Equals(other);

    public override int GetHashCode() => unchecked((X * 397) ^ Y);

    public int CompareTo(GridPosition other)
    {
        int yComparison = Y.CompareTo(other.Y);
        return yComparison != 0 ? yComparison : X.CompareTo(other.X);
    }

    public override string ToString() => $"({X},{Y})";

    public static bool operator ==(GridPosition left, GridPosition right) => left.Equals(right);

    public static bool operator !=(GridPosition left, GridPosition right) => !left.Equals(right);
}

/// <summary>Stores movement and cover properties for one grid cell.</summary>
public readonly struct TerrainCell
{
    public TerrainCell(bool isWalkable, int moveCost = 1, int cover = 0)
    {
        if (moveCost < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(moveCost), "Move cost must be positive.");
        }

        if (cover < 0 || cover > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(cover), "Cover must be between 0 and 100.");
        }

        IsWalkable = isWalkable;
        MoveCost = moveCost;
        Cover = cover;
    }

    public bool IsWalkable { get; }

    public int MoveCost { get; }

    public int Cover { get; }
}

/// <summary>Owns a fixed-size rectangular terrain grid.</summary>
public sealed class GridMap
{
    private readonly TerrainCell[] cells;

    public GridMap(int width, int height, IEnumerable<TerrainCell>? cells = null)
    {
        if (width < 2 || height < 2 || width > 128 || height > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Map dimensions must be between 2 and 128.");
        }

        Width = width;
        Height = height;
        this.cells = cells?.ToArray() ?? Enumerable.Repeat(new TerrainCell(true), width * height).ToArray();
        if (this.cells.Length != width * height)
        {
            throw new ArgumentException("Cell count must equal width multiplied by height.", nameof(cells));
        }
    }

    public int Width { get; }

    public int Height { get; }

    public TerrainCell this[GridPosition position]
    {
        get
        {
            ValidatePosition(position);
            return cells[(position.Y * Width) + position.X];
        }
        set
        {
            ValidatePosition(position);
            cells[(position.Y * Width) + position.X] = value;
        }
    }

    /// <summary>Returns whether a coordinate lies inside the grid.</summary>
    public bool Contains(GridPosition position) =>
        position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;

    /// <summary>Returns a defensive copy of cells in row-major order.</summary>
    public TerrainCell[] CopyCells() => (TerrainCell[])cells.Clone();

    /// <summary>Creates an independent map copy.</summary>
    public GridMap Clone() => new(Width, Height, cells);

    private void ValidatePosition(GridPosition position)
    {
        if (!Contains(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), $"{position} is outside the map.");
        }
    }
}

/// <summary>Describes a data-driven combat ability.</summary>
public sealed class AbilityDefinition
{
    public AbilityDefinition(string id, int damage, int range, int cooldown)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("An ability identifier is required.", nameof(id));
        }

        if (damage < 0 || range < 1 || cooldown < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(damage), "Ability values must be non-negative and range must be positive.");
        }

        Id = id;
        Damage = damage;
        Range = range;
        Cooldown = cooldown;
    }

    public string Id { get; }

    public int Damage { get; }

    public int Range { get; }

    public int Cooldown { get; }
}

/// <summary>Stores one combatant and its per-ability cooldowns.</summary>
public sealed class UnitState
{
    private readonly SortedDictionary<string, int> cooldowns;

    public UnitState(string id, Team team, GridPosition position, int hitPoints, int maxHitPoints)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A unit identifier is required.", nameof(id));
        }

        if (maxHitPoints < 1 || hitPoints < 0 || hitPoints > maxHitPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(hitPoints), "Hit points must be within the unit maximum.");
        }

        Id = id;
        Team = team;
        Position = position;
        HitPoints = hitPoints;
        MaxHitPoints = maxHitPoints;
        cooldowns = new SortedDictionary<string, int>(StringComparer.Ordinal);
    }

    public string Id { get; }

    public Team Team { get; }

    public GridPosition Position { get; private set; }

    public int HitPoints { get; private set; }

    public int MaxHitPoints { get; }

    public bool IsAlive => HitPoints > 0;

    public IReadOnlyDictionary<string, int> Cooldowns => cooldowns;

    /// <summary>Moves the unit after engine validation.</summary>
    public void MoveTo(GridPosition position) => Position = position;

    /// <summary>Applies bounded damage and returns the actual damage dealt.</summary>
    public int ApplyDamage(int damage)
    {
        if (damage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(damage));
        }

        int previous = HitPoints;
        HitPoints = Math.Max(0, HitPoints - damage);
        return previous - HitPoints;
    }

    /// <summary>Returns the remaining cooldown for an ability.</summary>
    public int GetCooldown(string abilityId) => cooldowns.TryGetValue(abilityId, out int value) ? value : 0;

    /// <summary>Sets a cooldown after an ability resolves.</summary>
    public void SetCooldown(string abilityId, int turns)
    {
        if (turns > 0)
        {
            cooldowns[abilityId] = turns;
        }
        else
        {
            cooldowns.Remove(abilityId);
        }
    }

    /// <summary>Reduces every active cooldown by one turn.</summary>
    public void TickCooldowns()
    {
        foreach (string key in cooldowns.Keys.ToArray())
        {
            SetCooldown(key, cooldowns[key] - 1);
        }
    }

    /// <summary>Creates an independent unit copy.</summary>
    public UnitState Clone()
    {
        var copy = new UnitState(Id, Team, Position, HitPoints, MaxHitPoints);
        foreach (KeyValuePair<string, int> entry in cooldowns)
        {
            copy.cooldowns.Add(entry.Key, entry.Value);
        }

        return copy;
    }
}

/// <summary>Owns all mutable state required to resolve a deterministic match.</summary>
public sealed class TacticalState
{
    private readonly List<UnitState> units;

    public TacticalState(GridMap map, IEnumerable<UnitState> units, Team activeTeam = Team.Blue, int turnNumber = 1)
    {
        Map = map?.Clone() ?? throw new ArgumentNullException(nameof(map));
        this.units = units?.Select(unit => unit.Clone()).OrderBy(unit => unit.Id, StringComparer.Ordinal).ToList()
            ?? throw new ArgumentNullException(nameof(units));
        if (this.units.Count < 2 || this.units.Select(unit => unit.Id).Distinct(StringComparer.Ordinal).Count() != this.units.Count)
        {
            throw new ArgumentException("A match requires at least two uniquely named units.", nameof(units));
        }

        if (!this.units.Any(unit => unit.Team == Team.Blue) || !this.units.Any(unit => unit.Team == Team.Red))
        {
            throw new ArgumentException("A match requires at least one unit from each team.", nameof(units));
        }

        if (this.units.Select(unit => unit.Position).Distinct().Count() != this.units.Count)
        {
            throw new ArgumentException("Units cannot share a starting cell.", nameof(units));
        }

        if (this.units.Any(unit => !Map.Contains(unit.Position) || !Map[unit.Position].IsWalkable))
        {
            throw new ArgumentException("Every unit must begin on a walkable map cell.", nameof(units));
        }

        ActiveTeam = activeTeam;
        TurnNumber = turnNumber;
    }

    public GridMap Map { get; }

    public IReadOnlyList<UnitState> Units => units;

    public Team ActiveTeam { get; private set; }

    public int TurnNumber { get; private set; }

    public Team? Winner
    {
        get
        {
            bool blueAlive = units.Any(unit => unit.IsAlive && unit.Team == Team.Blue);
            bool redAlive = units.Any(unit => unit.IsAlive && unit.Team == Team.Red);
            return blueAlive == redAlive ? null : blueAlive ? Team.Blue : Team.Red;
        }
    }

    /// <summary>Finds a unit by its stable identifier.</summary>
    public UnitState? FindUnit(string id) => units.FirstOrDefault(unit => string.Equals(unit.Id, id, StringComparison.Ordinal));

    /// <summary>Returns whether a living unit occupies a coordinate.</summary>
    public bool IsOccupied(GridPosition position, string? exceptUnitId = null) => units.Any(unit =>
        unit.IsAlive && unit.Position == position && !string.Equals(unit.Id, exceptUnitId, StringComparison.Ordinal));

    /// <summary>Ends the active side's action and advances all cooldowns for the incoming side.</summary>
    public void AdvanceTurn()
    {
        ActiveTeam = ActiveTeam == Team.Blue ? Team.Red : Team.Blue;
        TurnNumber++;
        foreach (UnitState unit in units.Where(unit => unit.Team == ActiveTeam && unit.IsAlive))
        {
            unit.TickCooldowns();
        }
    }

    /// <summary>Creates a deep copy suitable for replay validation and AI search.</summary>
    public TacticalState Clone() => new(Map, units, ActiveTeam, TurnNumber);
}

/// <summary>Represents one serializable player or AI decision.</summary>
public sealed class ActionCommand
{
    public ActionCommand(ActionKind kind, string actorId, GridPosition targetPosition, string? targetUnitId = null, string? abilityId = null)
    {
        Kind = kind;
        ActorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
        TargetPosition = targetPosition;
        TargetUnitId = targetUnitId;
        AbilityId = abilityId;
    }

    public ActionKind Kind { get; }

    public string ActorId { get; }

    public GridPosition TargetPosition { get; }

    public string? TargetUnitId { get; }

    public string? AbilityId { get; }

    /// <summary>Returns a stable key used for deterministic tie-breaking.</summary>
    public string StableKey() => $"{(int)Kind:D2}|{ActorId}|{AbilityId}|{TargetUnitId}|{TargetPosition.Y:D4}|{TargetPosition.X:D4}";
}

/// <summary>Describes one observable outcome from a resolved command.</summary>
public sealed class ActionEvent
{
    public ActionEvent(string type, string message, int value = 0)
    {
        Type = type;
        Message = message;
        Value = value;
    }

    public string Type { get; }

    public string Message { get; }

    public int Value { get; }
}

/// <summary>Reports command acceptance and emitted events.</summary>
public sealed class ActionResult
{
    private ActionResult(bool succeeded, string error, IReadOnlyList<ActionEvent> events)
    {
        Succeeded = succeeded;
        Error = error;
        Events = events;
    }

    public bool Succeeded { get; }

    public string Error { get; }

    public IReadOnlyList<ActionEvent> Events { get; }

    public static ActionResult Success(params ActionEvent[] events) => new(true, string.Empty, events);

    public static ActionResult Failure(string error) => new(false, error, Array.Empty<ActionEvent>());
}
