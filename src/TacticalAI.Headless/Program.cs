// File: Program.cs
// Purpose: Expose deterministic demos, replay checks, and batch simulations as a headless CLI.
// Public API: Process entry point and documented command-line verbs.
// Variables: Parsed options and output paths are scoped to the invoked command.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using System.Globalization;
using TacticalAI.Core;
using TacticalAI.Serialization;

TacticalEngine engine = CreateEngine();
try
{
    return args.Length == 0 || args[0] == "demo"
        ? RunDemo(engine)
        : args[0] switch
        {
            "simulate" => RunSimulation(engine, args[1..]),
            "replay" => ValidateReplay(engine, args[1..]),
            "validate-save" => ValidateSave(args[1..]),
            _ => PrintUsage(),
        };
}
catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"error: {exception.Message}");
    return 2;
}

static TacticalEngine CreateEngine() => new(new[]
{
    new AbilityDefinition("pulse-shot", damage: 34, range: 5, cooldown: 1),
    new AbilityDefinition("close-burst", damage: 52, range: 2, cooldown: 2),
});

static int RunDemo(TacticalEngine engine)
{
    const ulong seed = 42;
    var runner = new SimulationRunner(engine);
    MatchResult result = runner.RunMatch(seed);
    Console.WriteLine("Tactical AI deterministic demo");
    Console.WriteLine($"seed={result.Seed} winner={result.Winner?.ToString() ?? "Draw"} turns={result.Turns} damage={result.Damage}");
    Console.WriteLine($"finalHash={result.FinalHash}");
    return 0;
}

static int RunSimulation(TacticalEngine engine, string[] arguments)
{
    int matches = ReadInt(arguments, "--matches", 100, 1, 10_000);
    ulong seed = ReadUInt64(arguments, "--seed", 1UL);
    string outputDirectory = ReadString(arguments, "--out", "artifacts/simulation");
    Directory.CreateDirectory(outputDirectory);

    var runner = new SimulationRunner(engine);
    SimulationSummary summary = runner.RunBatch(seed, matches);
    File.WriteAllText(Path.Combine(outputDirectory, "balance-report.json"), JsonCodec.SerializeTelemetry(summary, seed));
    File.WriteAllText(Path.Combine(outputDirectory, "balance-report.csv"), JsonCodec.SerializeTelemetryCsv(summary));

    TacticalState initial = EncounterGenerator.Create(seed);
    var agent = new UtilityAgent();
    var commands = new List<ActionCommand>();
    TacticalState working = initial.Clone();
    while (!working.Winner.HasValue && commands.Count < 40)
    {
        ActionCommand command = agent.ChooseAction(working, engine).Command;
        ActionResult applied = engine.Apply(working, command);
        if (!applied.Succeeded)
        {
            throw new InvalidOperationException(applied.Error);
        }

        commands.Add(command);
    }

    ReplayDocument replay = ReplayService.Record(initial, engine, commands);
    File.WriteAllText(Path.Combine(outputDirectory, "sample-replay.json"), JsonCodec.SerializeReplay(replay));
    Console.WriteLine($"matches={summary.Matches} blue={summary.BlueWins} red={summary.RedWins} draws={summary.Draws} averageTurns={summary.AverageTurns:F2}");
    Console.WriteLine($"reports={Path.GetFullPath(outputDirectory)}");
    return 0;
}

static int ValidateReplay(TacticalEngine engine, string[] arguments)
{
    if (arguments.Length != 1)
    {
        throw new ArgumentException("replay requires exactly one JSON file path.");
    }

    ReplayDocument replay = JsonCodec.DeserializeReplay(File.ReadAllText(arguments[0]));
    ReplayValidation validation = ReplayService.Validate(replay, engine);
    Console.WriteLine($"valid={validation.Succeeded} steps={validation.StepsValidated} finalHash={validation.FinalHash}");
    if (!validation.Succeeded)
    {
        Console.Error.WriteLine(validation.Error);
    }

    return validation.Succeeded ? 0 : 1;
}

static int ValidateSave(string[] arguments)
{
    if (arguments.Length != 1)
    {
        throw new ArgumentException("validate-save requires exactly one JSON file path.");
    }

    TacticalState state = JsonCodec.DeserializeSave(File.ReadAllText(arguments[0]));
    Console.WriteLine($"valid=True units={state.Units.Count} turn={state.TurnNumber} stateHash={StateHasher.Compute(state)}");
    return 0;
}

static int PrintUsage()
{
    Console.Error.WriteLine("Usage: TacticalAI.Headless [demo | simulate --matches N --seed N --out PATH | replay FILE | validate-save FILE]");
    return 2;
}

static string ReadString(string[] arguments, string option, string fallback)
{
    int index = Array.IndexOf(arguments, option);
    if (index < 0)
    {
        return fallback;
    }

    if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
    {
        throw new ArgumentException($"{option} requires a value.");
    }

    return arguments[index + 1];
}

static int ReadInt(string[] arguments, string option, int fallback, int minimum, int maximum)
{
    string text = ReadString(arguments, option, fallback.ToString(CultureInfo.InvariantCulture));
    if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < minimum || value > maximum)
    {
        throw new ArgumentException($"{option} must be between {minimum} and {maximum}.");
    }

    return value;
}

static ulong ReadUInt64(string[] arguments, string option, ulong fallback)
{
    string text = ReadString(arguments, option, fallback.ToString(CultureInfo.InvariantCulture));
    if (!ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value))
    {
        throw new ArgumentException($"{option} must be an unsigned 64-bit integer.");
    }

    return value;
}
