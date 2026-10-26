using System;
using System.IO;
using System.Linq;
using MysteryGame.Companions;
using UnityEditor;
using UnityEngine;

public static class CompanionPlaytestTools
{
    [MenuItem("Mystery Game/Playtest/Start Local Trace")]
    public static void StartTrace()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Enter Play before starting a playtest trace."); return; }
        if (CompanionPlaytestTrace.Begin()) Debug.Log("Playtest trace: " + CompanionPlaytestTrace.LastPath);
    }
    [MenuItem("Mystery Game/Playtest/Stop Local Trace")]
    public static void StopTrace() { CompanionPlaytestTrace.End(); }

    [MenuItem("Mystery Game/Playtest/Build Offline Windows Demo")]
    public static void BuildOfflineDemo()
    {
        BuildDemo(true);
    }

    [MenuItem("Mystery Game/Playtest/Build AI-Enabled Windows Demo")]
    public static void BuildAiEnabledDemo()
    {
        BuildDemo(false);
    }

    private static void BuildDemo(bool offline)
    {
        // extraScriptingDefines cannot remove project-wide symbols. Fail clearly
        // rather than accidentally shipping an offline executable as the AI build.
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
        if (!offline && defines.Split(';').Select(d => d.Trim()).Contains("NPC_PLAYTEST_BUILD"))
            throw new InvalidOperationException("Remove NPC_PLAYTEST_BUILD from Standalone project defines before building the AI-enabled demo.");
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) throw new InvalidOperationException("No enabled build scenes.");
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath,
            offline ? "../Logs/CompanionPlaytestBuilds" : "../Logs/CompanionAiBuilds",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6)));
        Directory.CreateDirectory(folder);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = scenes,
            locationPathName = Path.Combine(folder, "MysteryGame.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = offline ? BuildOptions.Development : BuildOptions.None,
            extraScriptingDefines = offline ? new[] { "NPC_PLAYTEST_BUILD" } : Array.Empty<string>()
        });
        string label = offline ? "Offline playtest build" : "AI-enabled demo build";
        Debug.Log(label + ": " + report.summary.result + ", errors=" + report.summary.totalErrors + ", path=" + folder);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException(label + " failed.");
        File.WriteAllText(Path.Combine(folder, offline ? "PLAYTEST_README.txt" : "README_AI.txt"), offline ?
            "Offline Room01 playtest build. Start MysteryGame.exe, select New Game.\r\n" +
            "AI requests are disabled for this build. Existing saves remain in the game's usual save folder.\r\n" +
            "Opt-in trace contains event IDs and timings only, no dialogue or API keys.\r\n" +
            "This development build records traces automatically in the application's persistentDataPath/Playtests.\r\n" +
            "Close the game before collecting the jsonl file. Do not treat automated build success as manual QA.\r\n" :
            "AI-enabled Windows demo. Extract the whole ZIP, then start MysteryGame.exe.\r\n" +
            "Open Settings (F10), select your provider, model and key, then Save and Test connection.\r\n" +
            "Saving does not call the API. Testing and playing with AI use your provider quota.\r\n" +
            "The typed key is used for this run only; model and endpoint are saved. No key is bundled.\r\n" +
            "For local KKU key loading on later runs, create UserSettings/kku_api_key.txt next to the exe yourself.\r\n" +
            "NPC_PLAYTEST_BUILD is not enabled. Playtest trace is off by default.\r\n" +
            "No real provider was called to validate this package; verify your account/model with Test connection.\r\n");
    }
}
