using System;

// Formato salvo no PlayerPrefs (como JSON, via JsonUtility).
[Serializable]
public class EvolutionSnapshot
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int seed;
    public int generation;
    public float bestFitness;
    public float[] weights;
    public int populationSize;
    public float mutationRate;
    public float mutationStrength;
    public string savedAt;
}
