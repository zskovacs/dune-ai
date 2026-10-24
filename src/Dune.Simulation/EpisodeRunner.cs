using Dune.Engine;

namespace Dune.Simulation;

/// <summary>Runs bounded autonomous episodes and batches ordered by input seed.</summary>
/// <remarks>
/// Factories must return a fresh game and fresh agents per player per episode, and be safe for concurrent calls.
/// Configuration, observations, actions and results must be immutable or detached snapshots; the runner cannot
/// deep-copy arbitrary generic values. Agents never receive a game or replay reference. Do not share mutable
/// configuration across batch episodes. Exceptions from parallel episodes follow Parallel.For aggregation semantics.
/// </remarks>
public sealed class EpisodeRunner<TConfig, TObservation, TAction, TResult>
{
    private readonly Func<IGame<TConfig, TObservation, TAction, TResult>> _gameFactory;
    private readonly Func<int, int, IAgent<TObservation, TAction>> _agentFactory;

    /// <param name="gameFactory">Creates an uninitialized game owned by one episode.</param>
    /// <param name="agentFactory">Creates an agent from zero-based player ID and a policy-only RNG seed; no environment seed is supplied.</param>
    public EpisodeRunner(Func<IGame<TConfig, TObservation, TAction, TResult>> gameFactory,
        Func<int, int, IAgent<TObservation, TAction>> agentFactory)
    {
        ArgumentNullException.ThrowIfNull(gameFactory);
        ArgumentNullException.ThrowIfNull(agentFactory);
        _gameFactory = gameFactory;
        _agentFactory = agentFactory;
    }

    /// <summary>Runs a game to termination; throws if it exceeds the transition bound.</summary>
    /// <param name="seed">Environment seed, passed unchanged to the engine.</param>
    /// <param name="config">Immutable or independently owned engine configuration.</param>
    /// <param name="agentSeed">Policy seed chosen independently of the private environment seed; System.Random derives player seeds in player order.</param>
    /// <param name="maxSteps">Maximum permitted engine transitions.</param>
    /// <param name="cancellationToken">Checked before setup and each transition; also supplied to policies.</param>
    public EpisodeResult<TAction, TResult> Run(int seed, TConfig config, int agentSeed = 0,
        int maxSteps = 10_000, CancellationToken cancellationToken = default)
    {
        ValidateArguments(config, maxSteps);
        cancellationToken.ThrowIfCancellationRequested();
        var game = _gameFactory() ?? throw new InvalidOperationException("The game factory returned null.");
        game.Reset(seed, config);
        if (game.PlayerCount <= 0)
        {
            throw new InvalidOperationException("A game must have at least one player.");
        }

        var policyRandom = new Random(agentSeed);
        var playerSeeds = new int[game.PlayerCount];
        for (var playerId = 0; playerId < playerSeeds.Length; playerId++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            playerSeeds[playerId] = policyRandom.Next();
        }

        var agents = new IAgent<TObservation, TAction>[game.PlayerCount];
        var uniqueAgents = new HashSet<IAgent<TObservation, TAction>>(ReferenceEqualityComparer.Instance);
        for (var playerId = 0; playerId < agents.Length; playerId++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var agent = _agentFactory(playerId, playerSeeds[playerId])
                ?? throw new InvalidOperationException("The agent factory returned null.");
            if (!uniqueAgents.Add(agent))
            {
                throw new InvalidOperationException("Each player must own a separate agent instance.");
            }
            agents[playerId] = agent;
        }

        var actions = new List<TAction>();
        while (!game.IsTerminal)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (actions.Count >= maxSteps)
            {
                throw new InvalidOperationException($"Episode exceeded its limit of {maxSteps} transitions.");
            }

            if (game.DecisionOwnerId is not int playerId || playerId < 0 || playerId >= agents.Length)
            {
                throw new InvalidOperationException("A nonterminal game must expose a valid decision owner.");
            }

            var observation = game.Observe(playerId);
            var legalActions = Array.AsReadOnly(game.LegalActions(playerId).ToArray());
            if (legalActions.Count == 0)
            {
                throw new InvalidOperationException("The decision owner has no legal actions.");
            }

            var action = agents[playerId].SelectAction(observation, legalActions, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!legalActions.Contains(action))
            {
                throw new InvalidOperationException("The agent selected an action outside the legal action set.");
            }
            game.Step(action);
            actions.Add(action);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new EpisodeResult<TAction, TResult>(seed, agentSeed, actions, game.GetResult());
    }

    /// <summary>Runs independent episodes with bounded parallelism; output indices match seed indices.</summary>
    /// <param name="seeds">Private environment seeds; never used to derive policy seeds.</param>
    /// <param name="config">Immutable engine configuration shared across episodes.</param>
    /// <param name="degreeOfParallelism">Maximum number of concurrent episodes.</param>
    /// <param name="agentSeed">Public base policy seed. System.Random derives episode policy seeds in input index order before scheduling.</param>
    /// <param name="maxSteps">Maximum permitted engine transitions per episode.</param>
    /// <param name="cancellationToken">Cancellation propagated to every episode.</param>
    /// <remarks>Each result records its actual episode policy seed; pass it to Run to reproduce that episode's policies.</remarks>
    public IReadOnlyList<EpisodeResult<TAction, TResult>> RunBatch(IReadOnlyList<int> seeds, TConfig config,
        int degreeOfParallelism = 1, int agentSeed = 0, int maxSteps = 10_000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        ValidateArguments(config, maxSteps);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(degreeOfParallelism);
        cancellationToken.ThrowIfCancellationRequested();
        var seedSnapshot = seeds.ToArray();
        var policyRandom = new Random(agentSeed);
        var episodePolicySeeds = new int[seedSnapshot.Length];
        for (var index = 0; index < episodePolicySeeds.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            episodePolicySeeds[index] = policyRandom.Next();
        }

        var results = new EpisodeResult<TAction, TResult>[seedSnapshot.Length];
        Parallel.For(0, seedSnapshot.Length, new ParallelOptions
        {
            MaxDegreeOfParallelism = degreeOfParallelism,
            CancellationToken = cancellationToken
        }, index => results[index] = Run(seedSnapshot[index], config, episodePolicySeeds[index], maxSteps, cancellationToken));
        return Array.AsReadOnly(results);
    }

    private static void ValidateArguments(TConfig config, int maxSteps)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSteps);
    }
}
