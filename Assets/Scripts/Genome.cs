using System;

[Serializable]
public sealed class Genome
{
    public float[] Weights;
    public float Fitness;

    public Genome(int weightCount, Random random, float initialRange = 1f)
    {
        Weights = new float[weightCount];
        for (int i = 0; i < Weights.Length; i++)
            Weights[i] = RandomRange(random, -initialRange, initialRange);
    }

    private Genome(float[] weights)
    {
        Weights = weights;
    }

    public Genome Clone()
    {
        float[] copy = new float[Weights.Length];
        Array.Copy(Weights, copy, Weights.Length);
        return new Genome(copy) { Fitness = Fitness };
    }

    // Cria um genoma a partir de pesos existentes (ex.: save), sempre com copia do array.
    public static Genome FromWeights(float[] weights, float fitness)
    {
        float[] copy = new float[weights.Length];
        Array.Copy(weights, copy, weights.Length);
        return new Genome(copy) { Fitness = fitness };
    }

    public static Genome Crossover(Genome first, Genome second, Random random)
    {
        float[] child = new float[first.Weights.Length];
        for (int i = 0; i < child.Length; i++)
            child[i] = random.NextDouble() < 0.5 ? first.Weights[i] : second.Weights[i];

        return new Genome(child);
    }

    public void Mutate(Random random, float rate, float strength)
    {
        for (int i = 0; i < Weights.Length; i++)
        {
            if (random.NextDouble() < rate)
                Weights[i] += RandomRange(random, -strength, strength);
        }
    }

    private static float RandomRange(Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }
}
