using System;
using UnityEngine;

// Persistencia do melhor genoma em uma unica chave do PlayerPrefs.
public static class BestGenomeStore
{
    public const string Key = "neuroevolution.best.v1";

    public static bool HasSave => PlayerPrefs.HasKey(Key);

    public static void Save(EvolutionSnapshot snapshot)
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(snapshot));
        PlayerPrefs.Save(); // SetString sozinho nao grava em disco.
    }

    // Nunca lanca excecao: em qualquer problema retorna false e explica em message.
    public static bool TryLoad(out EvolutionSnapshot snapshot, out string message)
    {
        snapshot = null;

        if (!PlayerPrefs.HasKey(Key))
        {
            message = $"Nenhum save encontrado ('{Key}'). Use 'Salvar melhor' antes de carregar.";
            return false;
        }

        string json = PlayerPrefs.GetString(Key, string.Empty);
        EvolutionSnapshot loaded;
        try
        {
            loaded = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<EvolutionSnapshot>(json);
        }
        catch (Exception exception)
        {
            message = $"Save corrompido (JSON inválido): {exception.Message}";
            return false;
        }

        if (loaded == null)
        {
            message = "Save vazio ou corrompido.";
            return false;
        }

        if (loaded.version != EvolutionSnapshot.CurrentVersion)
        {
            message = $"Save recusado: versão {loaded.version}, esperada {EvolutionSnapshot.CurrentVersion}.";
            return false;
        }

        int savedCount = loaded.weights == null ? 0 : loaded.weights.Length;
        if (savedCount != NeuralNetwork.WeightCount)
        {
            message = $"Save incompatível: {savedCount} pesos no save, a rede espera {NeuralNetwork.WeightCount}.";
            return false;
        }

        foreach (float weight in loaded.weights)
        {
            if (float.IsNaN(weight) || float.IsInfinity(weight))
            {
                message = "Save recusado: pesos inválidos (NaN ou infinito).";
                return false;
            }
        }

        snapshot = loaded;
        message = "Save lido com sucesso.";
        return true;
    }

    // Retorna true se existia save para apagar.
    public static bool Delete()
    {
        bool existed = PlayerPrefs.HasKey(Key);
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
        return existed;
    }
}
