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
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/CompanionPlaytestBuilds",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6)));
        Directory.CreateDirectory(folder);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = Path.Combine(folder, "MysteryGame.exe"),
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development,
            extraScriptingDefines = new[] { "NPC_PLAYTEST_BUILD" }
        });
        Debug.Log("Offline playtest build: " + report.summary.result + ", errors=" + report.summary.totalErrors + ", path=" + folder);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("Offline playtest player build failed.");
        File.WriteAllText(Path.Combine(folder, "PLAYTEST_README.txt"),
            "Offline Room01 playtest build. Start MysteryGame.exe, select New Game.\r\n" +
            "AI requests are disabled for this build. Existing saves remain in the game's usual save folder.\r\n" +
            "Opt-in trace contains event IDs and timings only, no dialogue or API keys.\r\n" +
            "This development build records traces automatically in the application's persistentDataPath/Playtests.\r\n" +
            "Close the game before collecting the jsonl file. Do not treat automated build success as manual QA.\r\n");
    }
}
