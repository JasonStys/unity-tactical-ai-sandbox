// File: Simulation.cs
// Purpose: Run bounded bot matches and aggregate balance-oriented telemetry.
// Public API: MatchResult, SimulationSummary, and SimulationRunner.
// Variables: Per-match command/event counters are local and merged into immutable summaries.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

namespace TacticalAI.Core;

/// <summary>Captures one completed or bounded match.</summary>
public sealed class MatchResult
{
    public MatchResult(ulong seed, Team? winner, int turns, int commands, int damage, IReadOnlyDictionary<string, int> actionCounts, string finalHash)
    {
        Seed = seed;
        Winner = winner;
        Turns = turns;
        Commands = commands;
        Damage = damage;
        ActionCounts = actionCounts;
        FinalHash = finalHash;
    }

    public ulong Seed { get; }

    public Team? Winner { get; }

    public int Turns { get; }

    public int Commands { get; }

    public int Damage { get; }

    public IReadOnlyDictionary<string, int> ActionCounts { get; }

    public string FinalHash { get; }
}

/// <summary>Aggregates a seeded match batch for balancing and regression analysis.</summary>
public sealed class SimulationSummary
{
    public SimulationSummary(int matches, int blueWins, int redWins, int draws, double averageTurns, int totalDamage, IReadOnlyDictionary<string, int> actionCounts, IReadOnlyList<MatchResult> results)
    {
        Matches = matches;
        BlueWins = blueWins;
        RedWins = redWins;
        Draws = draws;
        AverageTurns = averageTurns;
        TotalDamage = totalDamage;
        ActionCounts = actionCounts;
        Results = results;
    }

    public int Matches { get; }

    public int BlueWins { get; }

    public int RedWins { get; }

    public int Draws { get; }

    public double AverageTurns { get; }

    public int TotalDamage { get; }

    public IReadOnlyDictionary<string, int> ActionCounts { get; }

    public IReadOnlyList<MatchResult> Results { get; }
}

/// <summary>Executes utility-agent matches with explicit turn and evaluation limits.</summary>
public sealed class SimulationRunner
{
    private readonly TacticalEngine engine;
    private readonly UtilityAgent agent;

    public SimulationRunner(TacticalEngine engine, UtilityAgent? agent = null)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        this.agent = agent ?? new UtilityAgent();
    }

    /// <summary>Runs one seeded match and records deterministic telemetry.</summary>
    public MatchResult RunMatch(ulong seed, int maximumTurns = 200, int evaluationBudget = 256)
    {
        if (maximumTurns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTurns));
        }

        TacticalState state = EncounterGenerator.Create(seed);
        var actionCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int damage = 0;
        int commands = 0;

        while (!state.Winner.HasValue && commands < maximumTurns)
        {
            UtilityDecision decision = agent.ChooseAction(state, engine, evaluationBudget);
            ActionResult result = engine.Apply(state, decision.Command);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"The utility agent selected an invalid command: {result.Error}");
            }

            string actionName = decision.Command.Kind.ToString();
            actionCounts[actionName] = actionCounts.TryGetValue(actionName, out int count) ? count + 1 : 1;
            damage += result.Events.Where(value => value.Type == "damage").Sum(value => value.Value);
            commands++;
        }

        return new MatchResult(seed, state.Winner, state.TurnNumber, commands, damage, actionCounts, StateHasher.Compute(state));
    }

    /// <summary>Runs consecutive seeds and returns batch statistics.</summary>
    public SimulationSummary RunBatch(ulong firstSeed, int matchCount, int maximumTurns = 200)
    {
        if (matchCount < 1 || matchCount > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(matchCount));
        }

        var results = new List<MatchResult>(matchCount);
        var actions = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < matchCount; index++)
        {
            MatchResult result = RunMatch(firstSeed + (ulong)index, maximumTurns);
            results.Add(result);
            foreach (KeyValuePair<string, int> action in result.ActionCounts)
            {
                actions[action.Key] = actions.TryGetValue(action.Key, out int count) ? count + action.Value : action.Value;
            }
        }

        int blueWins = results.Count(result => result.Winner == Team.Blue);
        int redWins = results.Count(result => result.Winner == Team.Red);
        return new SimulationSummary(
            matchCount,
            blueWins,
            redWins,
            matchCount - blueWins - redWins,
            results.Average(result => result.Turns),
            results.Sum(result => result.Damage),
            actions,
            results);
    }
}
