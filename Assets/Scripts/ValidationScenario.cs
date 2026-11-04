using System;
using UnityEngine;

// Cenario de validacao configurado no Inspector do EvolutionManager.
[Serializable]
public class ValidationScenario
{
    public string name = "Cenario";

    [Tooltip("Opcional: posicao do Goal neste cenario. Vazio = mesma posicao do treino.")]
    public Transform goalMarker;

    [Tooltip("Opcional: objeto pai com obstaculos (layer Obstacle) ativado so durante este cenario.")]
    public GameObject layoutRoot;
}

public readonly struct ScenarioResult
{
    public readonly string Name;
    public readonly bool HasReferenceRoute;
    public readonly EpisodeMetrics Metrics;

    public ScenarioResult(string name, bool hasReferenceRoute, EpisodeMetrics metrics)
    {
        Name = name;
        HasReferenceRoute = hasReferenceRoute;
        Metrics = metrics;
    }

    public bool Success => Metrics.ReachedGoal && !Metrics.Collided;
}
