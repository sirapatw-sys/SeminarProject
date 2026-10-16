using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MysteryGame.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MysteryGame.Companions
{
    // Opt-in diagnostic output. No dialogue, prompts, evidence text or keys.
    public static class CompanionPlaytestTrace
    {
        [Serializable] private class Entry
        {
            public int schema = 1;
            public string session, build, kind, room, npc, target, request, outcome, route;
            public float seconds, duration;
        }
        private static StreamWriter writer;
        private static GameState observedState;
        private static string session, lastRoom;
        private static float started;
        private static int count;
        private static readonly Regex Id = new Regex(@"^[a-zA-Z0-9_.:/%-]{0,100}$", RegexOptions.CultureInvariant);
        public static bool Enabled => writer != null;
        public static bool OfflineMode { get; private set; }
        public static string LastPath { get; private set; }
        private static string Token(string value) => value != null && Id.IsMatch(value) ? value : "";
        public static bool Begin(string directory = null)
        {
            End();
            try
            {
                session = Guid.NewGuid().ToString("N"); count = 0; started = Time.realtimeSinceStartup;
                string folder = directory ?? Path.Combine(Application.persistentDataPath, "Playtests");
                Directory.CreateDirectory(folder);
                LastPath = Path.Combine(folder, "npc-" + session + ".jsonl");
                writer = new StreamWriter(new FileStream(LastPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
                writer.AutoFlush = true;
                SceneManager.sceneLoaded += SceneLoaded;
                InteractionSignals.Completed += Interaction;
                Application.quitting += End;
                BindState();
                lastRoom = SceneManager.GetActiveScene().name;
                Record("session_start", lastRoom);
                return true;
            }
            catch (Exception error) { Fail(error); return false; }
        }
        public static void End()
        {
            if (writer != null && count < 20000) Record("session_end", lastRoom);
            SceneManager.sceneLoaded -= SceneLoaded; InteractionSignals.Completed -= Interaction; Application.quitting -= End;
            if (observedState != null) { observedState.StateReset -= Reset; observedState.StateRestored -= Restored; }
            observedState = null;
            var closing = writer; writer = null;
            try { closing?.Dispose(); } catch (Exception error) { Debug.LogWarning("Playtest trace closed: " + error.GetType().Name); }
        }
        public static void Record(string kind, string room = null, string npc = null, string target = null,
            string request = null, string outcome = null, string route = null, float duration = 0f)
        {
            if (writer == null) return;
            BindState();
            if (count >= 20000) { End(); return; }
            try
            {
                writer.WriteLine(JsonUtility.ToJson(new Entry { session = session, build = Token(string.IsNullOrEmpty(Application.buildGUID) ? "editor-" + Application.version : Application.buildGUID),
                    kind = Token(kind), room = Token(room), npc = Token(npc), target = Token(target), request = Token(request),
                    outcome = Token(outcome), route = Token(route), seconds = Time.realtimeSinceStartup - started,
                    duration = float.IsNaN(duration) || float.IsInfinity(duration) ? 0f : Mathf.Max(0f, duration) }));
                count++;
            }
            catch (Exception error) { Fail(error); }
        }
        private static void BindState()
        {
            if (observedState == GameState.Instance) return;
            if (observedState != null) { observedState.StateReset -= Reset; observedState.StateRestored -= Restored; }
            observedState = GameState.Instance;
            if (observedState != null) { observedState.StateReset += Reset; observedState.StateRestored += Restored; }
        }
        private static void Reset() { Record("state_reset", GameState.Instance?.GetCurrentScene()); }
        private static void Restored() { Record("state_restore", GameState.Instance?.GetCurrentScene()); }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        { Record("scene_end", lastRoom); lastRoom = scene.name; Record("scene_enter", lastRoom); }
        private static void Interaction(InteractionData data, bool success)
        { if (data != null) Record("interaction", GameState.Instance?.GetCurrentScene(), target: data.interactionId, outcome: success ? "success" : "blocked"); }
        private static void Fail(Exception error)
        {
            var failed = writer; writer = null;
            try { failed?.Dispose(); } catch { }
            SceneManager.sceneLoaded -= SceneLoaded; InteractionSignals.Completed -= Interaction; Application.quitting -= End;
            if (observedState != null) { observedState.StateReset -= Reset; observedState.StateRestored -= Restored; }
            observedState = null;
            Debug.LogWarning("Playtest trace unavailable: " + error.GetType().Name);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime() { End(); LastPath = null; OfflineMode = false; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
#if NPC_PLAYTEST_BUILD
            OfflineMode = true;
            DialogueProviders.Override = new OfflineProvider();
            Begin();
#else
            if (Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--npc-playtest") >= 0)
            { OfflineMode = true; DialogueProviders.Override = new OfflineProvider(); Begin(); }
#endif
        }
        private sealed class OfflineProvider : IAiDialogueProvider
        {
            public bool CanGenerate => false;
            public string LastError => "";
            public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
            public IEnumerator GenerateReply(string npcId, string name, string context, string message,
                Action<GeneratedChatReply> done, string personality = null) { done(null); yield break; }
        }
    }
}
