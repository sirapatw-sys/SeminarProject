using System.Linq;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FrameworkTools
{
    public static T[] Assets<T>(string folder) where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
            .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
    }
    [MenuItem("Mystery Game/Validate Content")]
    public static void Validate()
    {
        var game = GameDefinition.Current;
        string folder = game != null && AssetDatabase.GetAssetPath(game).StartsWith("Assets/Samples/")
            ? System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(game)).Replace('\\', '/')
            : "Assets/Data";
        var errors = ContentValidator.Validate(game, Assets<InteractionData>(folder),
            Assets<InputPuzzleData>(folder), Assets<MiniEventData>(folder));
        if (game != null) foreach (var room in game.rooms)
            if (room != null && !EditorBuildSettings.scenes.Any(s => s.enabled && System.IO.Path.GetFileNameWithoutExtension(s.path) == room.roomId))
                errors.Add("Room missing from enabled Build Settings: " + room.roomId);
        foreach (string error in errors) Debug.LogError(error);
        if (errors.Count == 0) Debug.Log("Content validation passed.");
        if (Application.isBatchMode && errors.Count > 0) throw new System.InvalidOperationException("Content validation failed.");
    }
    [MenuItem("Mystery Game/Open Two-Room Sample")]
    public static void OpenSample()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene("Assets/Samples/Observatory/ObservatoryA.unity");
    }
    public static void BuildSmokePlayer()
    {
        string directory = System.IO.Path.Combine(Application.dataPath, "../Logs/FrameworkSmoke");
        System.IO.Directory.CreateDirectory(directory);
        var report = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = System.IO.Path.Combine(directory, "MysteryGame.exe"),
            target = UnityEditor.BuildTarget.StandaloneWindows64
        });
        Debug.Log("Framework player build: " + report.summary.result + ", errors=" + report.summary.totalErrors);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.InvalidOperationException("Player build failed.");
    }
}

public class NpcKnowledgeInspector : EditorWindow
{
    private int selected;
    private bool requestHint = true;
    private Vector2 scroll;
    [MenuItem("Mystery Game/NPC Knowledge Inspector")]
    private static void Open() { GetWindow<NpcKnowledgeInspector>("NPC Knowledge"); }
    private void OnInspectorUpdate() { Repaint(); }
    private void OnGUI()
    {
        var game = GameDefinition.Current;
        if (game == null) { EditorGUILayout.HelpBox("No GameDefinition.", MessageType.Warning); return; }
        var npcs = game.npcs.Where(n => n != null).ToArray();
        if (npcs.Length == 0) return;
        selected = Mathf.Clamp(selected, 0, npcs.Length - 1);
        selected = EditorGUILayout.Popup("NPC", selected, npcs.Select(n => n.npcId).ToArray());
        requestHint = EditorGUILayout.Toggle("Player requested hint", requestHint);
        var state = GameState.Instance;
        if (!Application.isPlaying || state == null)
        { EditorGUILayout.HelpBox("Enter Play Mode to inspect live knowledge.", MessageType.Info); return; }
        var context = NpcKnowledgeContextBuilder.Build(npcs[selected].npcId, state.GetCurrentScene(), state, requestHint);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Room", state.GetCurrentScene());
        EditorGUILayout.LabelField("Relationship", context.Relationship.ToString());
        EditorGUILayout.LabelField("Allowed hint", context.AllowedHintLevel.ToString());
        EditorGUILayout.LabelField("Next step", context.CurrentStep?.stepId ?? "(none available)");
        EditorGUILayout.HelpBox(NpcReplyPolicy.HintReply(context)?.reply ?? "No hint requested.", MessageType.Info);
        EditorGUILayout.LabelField("Shareable facts", EditorStyles.boldLabel);
        foreach (var fact in context.KnownFacts) EditorGUILayout.LabelField(fact.factId, fact.statement, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Withheld facts", EditorStyles.boldLabel);
        foreach (var fact in context.WithheldFacts) EditorGUILayout.LabelField(fact.factId);
        EditorGUILayout.LabelField("Locked secrets", EditorStyles.boldLabel);
        foreach (var fact in context.LockedSecrets) EditorGUILayout.LabelField(fact.factId);
        EditorGUILayout.EndScrollView();
    }
}
