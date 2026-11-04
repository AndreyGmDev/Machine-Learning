// Metricas de um episodio (uma nave, do spawn ate chegar, colidir ou estourar o tempo).
public readonly struct EpisodeMetrics
{
    private const float MinDistance = 0.001f;

    public readonly bool ReachedGoal;
    public readonly bool Collided;
    public readonly float TimeSeconds;
    public readonly float RealDistance;
    public readonly float ReferenceDistance;
    public readonly bool HasValidEfficiency;
    public readonly float Efficiency;

    public EpisodeMetrics(bool reachedGoal, bool collided, float timeSeconds, float realDistance, float referenceDistance)
    {
        ReachedGoal = reachedGoal;
        Collided = collided;
        TimeSeconds = timeSeconds;
        RealDistance = realDistance;
        ReferenceDistance = referenceDistance;

        // Eficiencia so e valida se chegou ao alvo e se as duas distancias permitem a divisao.
        HasValidEfficiency = reachedGoal && realDistance > MinDistance && referenceDistance > MinDistance;
        Efficiency = HasValidEfficiency ? referenceDistance / realDistance : 0f;
    }

    public string EfficiencyText => HasValidEfficiency ? Efficiency.ToString("F2") : "n/d";

    public override string ToString()
    {
        return $"chegou={ReachedGoal} colidiu={Collided} tempo={TimeSeconds:F1}s " +
               $"dist.real={RealDistance:F1} dist.ref={ReferenceDistance:F1} eficiencia={EfficiencyText}";
    }
}
