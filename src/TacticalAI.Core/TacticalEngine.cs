// File: TacticalEngine.cs
// Purpose: Validate and resolve movement, ability, and wait commands as atomic turns.
// Public API: TacticalEngine and its Apply method.
// Variables: Ability catalog is immutable after construction; events are scoped to one command.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

namespace TacticalAI.Core;

/// <summary>Applies validated commands to a tactical state.</summary>
public sealed class TacticalEngine
{
    private const int MaximumMoveDistance = 3;
    private readonly IReadOnlyDictionary<string, AbilityDefinition> abilities;

    public TacticalEngine(IEnumerable<AbilityDefinition> abilities)
    {
        if (abilities is null)
        {
            throw new ArgumentNullException(nameof(abilities));
        }

        this.abilities = abilities.ToDictionary(ability => ability.Id, StringComparer.Ordinal);
        if (this.abilities.Count == 0)
        {
            throw new ArgumentException("At least one ability is required.", nameof(abilities));
        }
    }

    public IReadOnlyDictionary<string, AbilityDefinition> Abilities => abilities;

    /// <summary>Validates and resolves exactly one command, advancing the turn only on success.</summary>
    public ActionResult Apply(TacticalState state, ActionCommand command)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (state.Winner.HasValue)
        {
            return ActionResult.Failure("The match has already ended.");
        }

        UnitState? actor = state.FindUnit(command.ActorId);
        if (actor is null || !actor.IsAlive)
        {
            return ActionResult.Failure("The acting unit does not exist or is defeated.");
        }

        if (actor.Team != state.ActiveTeam)
        {
            return ActionResult.Failure("The acting unit does not belong to the active team.");
        }

        ActionResult result = command.Kind switch
        {
            ActionKind.Move => ApplyMove(state, actor, command),
            ActionKind.UseAbility => ApplyAbility(state, actor, command),
            ActionKind.Wait => ActionResult.Success(new ActionEvent("wait", $"{actor.Id} waited.")),
            _ => ActionResult.Failure("The command kind is not supported."),
        };

        if (result.Succeeded)
        {
            state.AdvanceTurn();
        }

        return result;
    }

    private static ActionResult ApplyMove(TacticalState state, UnitState actor, ActionCommand command)
    {
        if (!state.Map.Contains(command.TargetPosition) || !state.Map[command.TargetPosition].IsWalkable)
        {
            return ActionResult.Failure("The destination is not walkable.");
        }

        if (state.IsOccupied(command.TargetPosition, actor.Id))
        {
            return ActionResult.Failure("The destination is occupied.");
        }

        PathResult path = GridAlgorithms.FindPath(
            state.Map,
            actor.Position,
            command.TargetPosition,
            position => state.IsOccupied(position, actor.Id));
        if (path.Path.Count == 0 || path.Path.Count - 1 > MaximumMoveDistance)
        {
            return ActionResult.Failure("The destination cannot be reached within the movement allowance.");
        }

        GridPosition origin = actor.Position;
        actor.MoveTo(command.TargetPosition);
        return ActionResult.Success(
            new ActionEvent("move", $"{actor.Id} moved from {origin} to {actor.Position}.", path.TotalCost),
            new ActionEvent("cover", $"{actor.Id} has {state.Map[actor.Position].Cover}% cover.", state.Map[actor.Position].Cover));
    }

    private ActionResult ApplyAbility(TacticalState state, UnitState actor, ActionCommand command)
    {
        if (command.AbilityId is null || !abilities.TryGetValue(command.AbilityId, out AbilityDefinition? ability))
        {
            return ActionResult.Failure("The requested ability is unknown.");
        }

        if (actor.GetCooldown(ability.Id) > 0)
        {
            return ActionResult.Failure("The requested ability is on cooldown.");
        }

        UnitState? target = command.TargetUnitId is null ? null : state.FindUnit(command.TargetUnitId);
        if (target is null || !target.IsAlive || target.Team == actor.Team)
        {
            return ActionResult.Failure("The target must be a living opposing unit.");
        }

        if (actor.Position.ManhattanDistance(target.Position) > ability.Range)
        {
            return ActionResult.Failure("The target is outside the ability range.");
        }

        if (!GridAlgorithms.HasLineOfSight(state.Map, actor.Position, target.Position))
        {
            return ActionResult.Failure("Terrain blocks line of sight.");
        }

        int cover = state.Map[target.Position].Cover;
        int mitigatedDamage = Math.Max(1, (ability.Damage * (100 - cover)) / 100);
        int actualDamage = target.ApplyDamage(mitigatedDamage);
        actor.SetCooldown(ability.Id, ability.Cooldown + 1);
        var events = new List<ActionEvent>
        {
            new("ability", $"{actor.Id} used {ability.Id} on {target.Id}.", actualDamage),
            new("damage", $"{target.Id} took {actualDamage} damage after {cover}% cover.", actualDamage),
        };

        if (!target.IsAlive)
        {
            events.Add(new ActionEvent("defeat", $"{target.Id} was defeated."));
        }

        return ActionResult.Success(events.ToArray());
    }
}
