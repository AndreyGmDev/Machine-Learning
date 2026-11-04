using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum RunMode
{
    Training,
    Playback,
    Validation,
    Stopped
}

public sealed class EvolutionManager : MonoBehaviour
{
    private const int RequiredScenarioSuccesses = 2;

    [Header("Cena")]
    [SerializeField] private EvolvingShip shipPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform goal;

    [Header("Evolucao")]
    [SerializeField, Min(4)] private int populationSize = 30;
    [SerializeField, Min(1)] private int eliteCount = 2;
    [SerializeField, Range(0f, 1f)] private float mutationRate = 0.08f;
    [SerializeField, Min(0f)] private float mutationStrength = 0.25f;
    [SerializeField, Min(1f)] private float episodeSeconds = 15f;
    [SerializeField] private int randomSeed = 12345;

    [Header("Fitness")]
    [SerializeField] private float progressWeight = 10f;
    [SerializeField] private float goalBonus = 100f;
    [SerializeField] private float collisionPenalty = -50f;
    [SerializeField] private float timePenalty = 1f;

    [Header("Rota de referencia (grid + A*)")]
    [SerializeField, Min(0.1f)] private float referenceCellSize = 0.5f;
    [SerializeField, Min(0f)] private float referenceAgentRadius = 0.5f;
    [SerializeField] private bool smoothReference = true;

    [Header("Validacao (cenarios fora do treino)")]
    [SerializeField] private List<ValidationScenario> scenarios = new();

    private readonly List<EvolvingShip> ships = new();
    private readonly List<Genome> population = new();
    private readonly List<ScenarioResult> validationResults = new();
    private System.Random random;
    private float elapsed;
    private int finishedCount;

    private Vector3 trainingGoalPosition;
    private ReferenceRoute reference;

    private Genome bestGenome;
    private int bestGenomeGeneration;

    private Genome validationGenome;
    private int scenarioIndex;
    private bool pendingAdvance;
    private EvolutionSnapshot playbackSnapshot;

    public int Generation { get; private set; }
    public float BestFitness { get; private set; }
    public float AverageFitness { get; private set; }
    public float ProgressWeight => progressWeight;
    public float GoalBonus => goalBonus;
    public float CollisionPenalty => collisionPenalty;
    public float TimePenalty => timePenalty;

    public RunMode Mode { get; private set; }
    public int RandomSeed => randomSeed;
    public string StatusMessage { get; private set; } = string.Empty;
    public EpisodeMetrics BestOfGenerationMetrics { get; private set; }
    public IReadOnlyList<ScenarioResult> ValidationResults => validationResults;
    public string ValidationVerdict { get; private set; } = string.Empty;

    // Comprimento da rota de referencia do cenario atual (0 se nao existe rota).
    public float ReferenceDistance => reference != null ? reference.Length : 0f;

    private void Start()
    {
        if (shipPrefab == null || spawnPoint == null || goal == null)
        {
            Debug.LogError("EvolutionManager precisa de Ship Prefab, Spawn Point e Goal.", this);
            enabled = false;
            return;
        }

        if (!TryGetComponent<EvolutionPanel>(out _))
            gameObject.AddComponent<EvolutionPanel>();

        trainingGoalPosition = goal.position;
        ApplyLayout(null);

        random = new System.Random(randomSeed);
        for (int i = 0; i < populationSize; i++)
            population.Add(new Genome(NeuralNetwork.WeightCount, random));

        Mode = RunMode.Training;
        BeginGeneration();
    }

    private void FixedUpdate()
    {
        // O proximo cenario comeca aqui (e nao dentro do callback de fisica que terminou o episodio).
        if (pendingAdvance)
        {
            pendingAdvance = false;
            AdvanceValidation();
            return;
        }

        if (Mode == RunMode.Stopped)
            return;

        elapsed += Time.fixedDeltaTime;
        if (elapsed >= episodeSeconds)
        {
            // A nova geração pode nascer ao finalizar o último agente; por isso
            // iteramos somente os agentes ativos capturados neste instante.
            EvolvingShip[] activeShips = ships.Where(ship => ship.IsActive).ToArray();
            foreach (EvolvingShip ship in activeShips)
                ship.Timeout();
        }
    }

    public void ReportFinished(EvolvingShip ship)
    {
        switch (Mode)
        {
            case RunMode.Training:
                finishedCount++;
                if (finishedCount >= ships.Count)
                    Evolve();
                break;
            case RunMode.Playback:
                FinishPlayback(ship);
                break;
            case RunMode.Validation:
                validationResults.Add(new ScenarioResult(scenarios[scenarioIndex].name, reference != null, ship.Metrics));
                pendingAdvance = true;
                break;
        }
    }

    // ---------- Treino ----------

    private void BeginGeneration()
    {
        elapsed = 0f;
        finishedCount = 0;

        EnsureShips(population.Count);

        for (int i = 0; i < population.Count; i++)
            ships[i].Begin(this, population[i], goal, spawnPoint.position, spawnPoint.rotation);
    }

    private void EnsureShips(int count)
    {
        while (ships.Count < count)
            ships.Add(Instantiate(shipPrefab, spawnPoint.position, spawnPoint.rotation, transform));
    }

    private void Evolve()
    {
        foreach (EvolvingShip ship in ships)
            ship.Genome.Fitness = ship.Fitness;

        population.Sort((a, b) => b.Fitness.CompareTo(a.Fitness));
        BestFitness = population[0].Fitness;
        AverageFitness = population.Average(genome => genome.Fitness);

        EvolvingShip bestShip = ships.Find(ship => ReferenceEquals(ship.Genome, population[0]));
        BestOfGenerationMetrics = bestShip.Metrics;
        Debug.Log($"Geracao {Generation}: melhor {BestFitness:F2}, media {AverageFitness:F2} | melhor da geracao: {BestOfGenerationMetrics}");

        // Guarda uma copia do melhor genoma ja visto (a populacao continua sendo alterada).
        if (bestGenome == null || BestFitness > bestGenome.Fitness)
        {
            bestGenome = population[0].Clone();
            bestGenomeGeneration = Generation;
        }

        var next = new List<Genome>(populationSize);
        int protectedCount = Mathf.Min(eliteCount, population.Count);
        for (int i = 0; i < protectedCount; i++)
            next.Add(population[i].Clone());

        while (next.Count < populationSize)
        {
            Genome parentA = TournamentPick();
            Genome parentB = TournamentPick();
            Genome child = Genome.Crossover(parentA, parentB, random);
            child.Mutate(random, mutationRate, mutationStrength);
            next.Add(child);
        }

        population.Clear();
        population.AddRange(next);
        Generation++;
        BeginGeneration();
    }

    private Genome TournamentPick()
    {
        const int tournamentSize = 3;
        Genome best = population[random.Next(population.Count)];
        for (int i = 1; i < tournamentSize; i++)
        {
            Genome contender = population[random.Next(population.Count)];
            if (contender.Fitness > best.Fitness)
                best = contender;
        }
        return best;
    }

    public void ResumeTraining()
    {
        if (Mode == RunMode.Training || Mode == RunMode.Validation)
            return;

        StopShips();
        Mode = RunMode.Training;
        BeginGeneration(); // reavalia a populacao atual; nao cria geracao nova.
        Report("Treino retomado.");
    }

    // ---------- Persistencia ----------

    public void SaveBest()
    {
        if (bestGenome == null)
        {
            Report("Ainda nao ha melhor genoma: aguarde o fim da primeira geracao.", true);
            return;
        }

        var snapshot = new EvolutionSnapshot
        {
            seed = randomSeed,
            generation = bestGenomeGeneration,
            bestFitness = bestGenome.Fitness,
            weights = (float[])bestGenome.Weights.Clone(),
            populationSize = populationSize,
            mutationRate = mutationRate,
            mutationStrength = mutationStrength,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        BestGenomeStore.Save(snapshot);
        Report($"Melhor genoma salvo: seed {snapshot.seed}, geracao {snapshot.generation}, fitness {snapshot.bestFitness:F2}.");
    }

    public void LoadBest()
    {
        if (Mode == RunMode.Validation)
        {
            Report("Aguarde o fim da validacao para carregar.", true);
            return;
        }

        if (!BestGenomeStore.TryLoad(out EvolutionSnapshot snapshot, out string message))
        {
            Report(message, true); // sem excecao e sem interromper o treino.
            return;
        }

        playbackSnapshot = snapshot;
        Genome loaded = Genome.FromWeights(snapshot.weights, snapshot.bestFitness);
        StopShips();
        Mode = RunMode.Playback;
        Report($"Save carregado (seed {snapshot.seed}, geracao {snapshot.generation}, fitness {snapshot.bestFitness:F2}, salvo em {snapshot.savedAt}). Executando sem criar geracao nova...");
        RunEpisode(loaded);
    }

    public void DeleteSave()
    {
        bool existed = BestGenomeStore.Delete();
        Report(existed
            ? $"Save apagado (PlayerPrefs.DeleteKey '{BestGenomeStore.Key}')."
            : $"Nao havia save para apagar (chave '{BestGenomeStore.Key}').");
    }

    private void FinishPlayback(EvolvingShip ship)
    {
        Mode = RunMode.Stopped;
        Report($"Genoma salvo executado (seed {playbackSnapshot.seed}, geracao {playbackSnapshot.generation}): " +
               $"{ship.Metrics} | fitness {ship.Fitness:F2} (salvo: {playbackSnapshot.bestFitness:F2}).");
    }

    // ---------- Validacao ----------

    public void RunValidation()
    {
        if (Mode == RunMode.Validation)
            return;

        if (bestGenome == null)
        {
            Report("Ainda nao ha melhor genoma para validar: aguarde o fim da primeira geracao.", true);
            return;
        }

        if (scenarios.Count == 0)
        {
            Report("Configure os cenarios de validacao no Inspector do EvolutionManager.", true);
            return;
        }

        StopShips();
        validationResults.Clear();
        ValidationVerdict = string.Empty;
        validationGenome = bestGenome.Clone();
        scenarioIndex = -1;
        Mode = RunMode.Validation;
        Report($"Validando o melhor genoma (seed {randomSeed}, geracao {bestGenomeGeneration}, fitness {validationGenome.Fitness:F2}) em {scenarios.Count} cenarios, sem evoluir...");
        AdvanceValidation();
    }

    private void AdvanceValidation()
    {
        scenarioIndex++;
        if (scenarioIndex >= scenarios.Count)
        {
            FinishValidation();
            return;
        }

        ApplyLayout(scenarios[scenarioIndex]);
        RunEpisode(validationGenome);
    }

    private void FinishValidation()
    {
        ApplyLayout(null); // volta ao cenario de treino.
        Mode = RunMode.Stopped;

        int successes = validationResults.Count(result => result.Success);
        bool validated = successes >= RequiredScenarioSuccesses;
        ValidationVerdict = $"{(validated ? "VALIDADO" : "NAO VALIDADO")}: {successes}/{validationResults.Count} cenarios sem colisao e com chegada (minimo {RequiredScenarioSuccesses}).";

        var table = new System.Text.StringBuilder();
        table.AppendLine($"Validacao do melhor genoma (seed {randomSeed}, geracao {bestGenomeGeneration}, fitness {bestGenome.Fitness:F2})");
        table.AppendLine($"{"Cenario",-20}{"Chegou",-8}{"Colidiu",-9}{"Tempo(s)",-10}{"Dist.real",-11}{"Dist.ref",-10}{"Eficiencia",-10}");
        foreach (ScenarioResult result in validationResults)
        {
            EpisodeMetrics m = result.Metrics;
            table.AppendLine($"{result.Name,-20}{m.ReachedGoal,-8}{m.Collided,-9}{m.TimeSeconds,-10:F1}{m.RealDistance,-11:F1}{m.ReferenceDistance,-10:F1}{m.EfficiencyText,-10}");
        }
        table.Append(ValidationVerdict);

        Report(table.ToString());
    }

    // ---------- Cenario / rota de referencia ----------

    // scenario == null restaura o cenario de treino.
    private void ApplyLayout(ValidationScenario scenario)
    {
        foreach (ValidationScenario item in scenarios)
        {
            if (item.layoutRoot != null)
                item.layoutRoot.SetActive(false);
        }

        Vector3 goalPosition = trainingGoalPosition;
        if (scenario != null)
        {
            if (scenario.layoutRoot != null)
                scenario.layoutRoot.SetActive(true);
            if (scenario.goalMarker != null)
                goalPosition = scenario.goalMarker.position;
        }

        goal.position = goalPosition;
        Physics.SyncTransforms();

        reference = ReferenceRoute.Find(spawnPoint.position, goal.position, shipPrefab.ObstacleMask, referenceCellSize, referenceAgentRadius, smoothReference);
        if (reference == null)
            Debug.LogWarning("Sem rota de referencia neste cenario: a eficiencia sera 'n/d'. O Goal esta alcancavel?", this);
    }

    // ---------- Utilitarios ----------

    private void RunEpisode(Genome genome)
    {
        elapsed = 0f;
        EnsureShips(1);
        ships[0].Begin(this, genome, goal, spawnPoint.position, spawnPoint.rotation);
    }

    private void StopShips()
    {
        foreach (EvolvingShip ship in ships)
            ship.Abort();
    }

    private void Report(string message, bool warning = false)
    {
        StatusMessage = message;
        if (warning)
            Debug.LogWarning(message, this);
        else
            Debug.Log(message, this);
    }

    private void OnDrawGizmos()
    {
        if (reference == null)
            return;

        Gizmos.color = Color.yellow;
        IReadOnlyList<Vector3> corners = reference.Corners;
        for (int i = 1; i < corners.Count; i++)
            Gizmos.DrawLine(corners[i - 1] + Vector3.up * 0.1f, corners[i] + Vector3.up * 0.1f);
    }
}
