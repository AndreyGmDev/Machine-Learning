using UnityEngine;

// Painel simples (IMGUI) com os botoes de Salvar/Carregar/Apagar/Validar e a tabela de validacao.
// E adicionado automaticamente pelo EvolutionManager.
public sealed class EvolutionPanel : MonoBehaviour
{
    private static readonly float[] ColumnWidths = { 130f, 60f, 65f, 65f, 80f, 75f, 80f };

    private EvolutionManager manager;

    private void Awake()
    {
        manager = GetComponent<EvolutionManager>();
    }

    private void OnGUI()
    {
        if (manager == null)
            return;

        float scale = Mathf.Max(1f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        GUILayout.BeginArea(new Rect(10f, 10f, 580f, Screen.height / scale - 20f), GUI.skin.box);

        GUILayout.Label($"Modo: {manager.Mode} | Seed: {manager.RandomSeed} | Geracao: {manager.Generation}");
        GUILayout.Label($"Fitness melhor: {manager.BestFitness:F2} | media: {manager.AverageFitness:F2}");
        GUILayout.Label($"Melhor da ultima geracao: {manager.BestOfGenerationMetrics}");
        GUILayout.Label($"Save existente: {(BestGenomeStore.HasSave ? "sim" : "nao")}");

        bool validating = manager.Mode == RunMode.Validation;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Salvar melhor"))
            manager.SaveBest();
        GUI.enabled = !validating;
        if (GUILayout.Button("Carregar melhor"))
            manager.LoadBest();
        GUI.enabled = true;
        if (GUILayout.Button("Apagar save"))
            manager.DeleteSave();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUI.enabled = !validating;
        if (GUILayout.Button("Validar nos cenarios"))
            manager.RunValidation();
        GUI.enabled = manager.Mode == RunMode.Playback || manager.Mode == RunMode.Stopped;
        if (GUILayout.Button("Retomar treino"))
            manager.ResumeTraining();
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        if (manager.ValidationResults.Count > 0)
            DrawValidationTable();

        if (!string.IsNullOrEmpty(manager.StatusMessage) && manager.ValidationResults.Count == 0)
            GUILayout.Label(manager.StatusMessage);

        GUILayout.EndArea();
    }

    private void DrawValidationTable()
    {
        string[] headers = { "Cenario", "Chegou", "Colidiu", "Tempo(s)", "Dist.real", "Dist.ref", "Eficiencia" };
        DrawRow(headers);
        foreach (ScenarioResult result in manager.ValidationResults)
        {
            EpisodeMetrics m = result.Metrics;
            DrawRow(new[]
            {
                result.Name, m.ReachedGoal ? "sim" : "nao", m.Collided ? "sim" : "nao",
                m.TimeSeconds.ToString("F1"), m.RealDistance.ToString("F1"), m.ReferenceDistance.ToString("F1"), m.EfficiencyText
            });
        }

        if (!string.IsNullOrEmpty(manager.ValidationVerdict))
            GUILayout.Label(manager.ValidationVerdict);
    }

    private static void DrawRow(string[] cells)
    {
        GUILayout.BeginHorizontal();
        for (int i = 0; i < cells.Length; i++)
            GUILayout.Label(cells[i], GUILayout.Width(ColumnWidths[i]));
        GUILayout.EndHorizontal();
    }
}
