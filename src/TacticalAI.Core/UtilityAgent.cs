// File: UtilityAgent.cs
// Purpose: Select deterministic tactical actions with explainable utility scores and hard budgets.
// Public API: UtilityWeights, UtilityDecision, and UtilityAgent.ChooseAction.
// Variables: Candidate lists and influence fields are local; stable keys resolve equal scores.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

namespace TacticalAI.Core;

/// <summary>Configures integer utility weights for reproducible decisions.</summary>
public sealed class UtilityWeights
{
    public int Damage { get; set; } = 10;

    public int Defeat { get; set; } = 1_000;

    public int Cover { get; set; } = 2;

    public int Distance { get; set; } = 30;

    public int Threat { get; set; } = 1;
}

/// <summary>Reports an AI command, its score, and diagnostic budget usage.</summary>
public sealed class UtilityDecision
{
    public UtilityDecision(ActionCommand command, int score, int candidatesEvaluated, bool budgetExhausted, string explanation)
    {
        Command = command;
        Score = score;
        CandidatesEvaluated = candidatesEvaluated;
        BudgetExhausted = budgetExhausted;
        Explanation = explanation;
    }

    public ActionCommand Command { get; }

    public int Score { get; }

    public int CandidatesEvaluated { get; }

    public bool BudgetExhausted { get; }

    public string Explanation { get; }
}

/// <summary>Builds and ranks bounded utility-AI candidates.</summary>
public sealed class UtilityAgent
{
    private readonly UtilityWeights weights;

    public UtilityAgent(UtilityWeights? weights = null)
    {
        this.weights = weights ?? new UtilityWeights();
    }

    /// <summary>Chooses the highest-scoring legal action with stable lexical tie-breaking.</summary>
    public UtilityDecision ChooseAction(TacticalState state, TacticalEngine engine, int evaluationBudget = 256)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (engine is null)
        {
            throw new ArgumentNullException(nameof(engine));
        }

        if (evaluationBudget < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(evaluationBudget));
        }

        UnitState[] allies = state.Units.Where(unit => unit.IsAlive && unit.Team == state.ActiveTeam)
            .OrderBy(unit => unit.Id, StringComparer.Ordinal).ToArray();
        UnitState[] enemies = state.Units.Where(unit => unit.IsAlive && unit.Team != state.ActiveTeam)
            .OrderBy(unit => unit.Id, StringComparer.Ordinal).ToArray();
        int[,] threat = GridAlgorithms.BuildInfluenceMap(state.Map, enemies.Select(unit => unit.Position), radius: 8);
        var candidates = new List<ScoredCandidate>();
        int evaluated = 0;
        bool exhausted = false;

        foreach (UnitState ally in allies)
        {
            foreach (UnitState enemy in enemies)
            {
                foreach (AbilityDefinition ability in engine.Abilities.Values.OrderBy(value => value.Id, StringComparer.Ordinal))
                {
                    if (evaluated >= evaluationBudget)
                    {
                        exhausted = true;
                        break;
                    }

                    if (ally.GetCooldown(ability.Id) == 0 &&
                        ally.Position.ManhattanDistance(enemy.Position) <= ability.Range &&
                        GridAlgorithms.HasLineOfSight(state.Map, ally.Position, enemy.Position))
                    {
                        int cover = state.Map[enemy.Position].Cover;
                        int damage = Math.Max(1, (ability.Damage * (100 - cover)) / 100);
                        int score = (damage * weights.Damage) + (damage >= enemy.HitPoints ? weights.Defeat : 0);
                        candidates.Add(new ScoredCandidate(
                            new ActionCommand(ActionKind.UseAbility, ally.Id, enemy.Position, enemy.Id, ability.Id),
                            score,
                            $"attack damage={damage}, defeatBonus={(damage >= enemy.HitPoints ? weights.Defeat : 0)}"));
                        evaluated++;
                    }
                }
            }

            if (exhausted)
            {
                break;
            }

            UnitState nearest = enemies.OrderBy(enemy => ally.Position.ManhattanDistance(enemy.Position))
                .ThenBy(enemy => enemy.Id, StringComparer.Ordinal).First();
            PathResult pursuit = GridAlgorithms.FindPath(
                state.Map,
                ally.Position,
                nearest.Position,
                position => state.IsOccupied(position, ally.Id),
                expansionBudget: 1_024);
            foreach (GridPosition destination in BuildMoveDestinations(state, ally, pursuit))
            {
                if (evaluated >= evaluationBudget)
                {
                    exhausted = true;
                    break;
                }

                int distance = destination.ManhattanDistance(nearest.Position);
                int score = (state.Map[destination].Cover * weights.Cover)
                    - (distance * weights.Distance)
                    - (threat[destination.X, destination.Y] * weights.Threat);
                candidates.Add(new ScoredCandidate(
                    new ActionCommand(ActionKind.Move, ally.Id, destination),
                    score,
                    $"move cover={state.Map[destination].Cover}, distance={distance}, threat={threat[destination.X, destination.Y]}"));
                evaluated++;
            }

            if (evaluated < evaluationBudget)
            {
                candidates.Add(new ScoredCandidate(
                    new ActionCommand(ActionKind.Wait, ally.Id, ally.Position),
                    -10_000,
                    "wait fallback"));
                evaluated++;
            }
        }

        ScoredCandidate selected = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Command.StableKey(), StringComparer.Ordinal)
            .First();
        return new UtilityDecision(selected.Command, selected.Score, evaluated, exhausted, selected.Explanation);
    }

    private static IEnumerable<GridPosition> BuildMoveDestinations(TacticalState state, UnitState ally, PathResult pursuit)
    {
        var destinations = new SortedSet<GridPosition>();
        foreach (GridPosition neighbor in ally.Position.CardinalNeighbors())
        {
            if (state.Map.Contains(neighbor) && state.Map[neighbor].IsWalkable && !state.IsOccupied(neighbor, ally.Id))
            {
                destinations.Add(neighbor);
            }
        }

        for (int index = 1; index < pursuit.Path.Count && index <= 3; index++)
        {
            GridPosition position = pursuit.Path[index];
            if (!state.IsOccupied(position, ally.Id))
            {
                destinations.Add(position);
            }
        }

        return destinations;
    }

    private sealed class ScoredCandidate
    {
        public ScoredCandidate(ActionCommand command, int score, string explanation)
        {
            Command = command;
            Score = score;
            Explanation = explanation;
        }

        public ActionCommand Command { get; }

        public int Score { get; }

        public string Explanation { get; }
    }
}
