using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Real modal paths and loopback HTTP only; never uses a real API key or the player's save.
public class ModalRequestRegressionTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    bool persistence, title, intro;
    class Offline : IAiDialogueProvider
    {
        public bool CanGenerate => false;
        public string LastError => "";
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message, Action<GeneratedChatReply> done, string personality = null) { done(null); yield break; }
    }
    [UnitySetUp]
    public IEnumerator Setup()
    {
        persistence = SaveSystem.PersistenceEnabled; SaveSystem.PersistenceEnabled = false;
        title = TitleMenu.IsOpen; intro = IntroSequence.IsPlaying;
        typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, false);
        typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, false);
        RoomTransitionManager.CloseRoomOverlays();
        DialogueProviders.Override = new Offline(); GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, false);
        typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, false);
        RoomTransitionManager.CloseRoomOverlays();
        foreach (var item in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) item.enabled = false;
        GameState.Instance.ResetState();
    }
    [TearDown]
    public void Cleanup()
    {
        RoomTransitionManager.CloseRoomOverlays(); DialogueProviders.Override = null;
        GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        SaveSystem.PersistenceEnabled = persistence;
        typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, title);
        typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, intro);
    }
    [UnityTest]
    public IEnumerator ItemPopupMustBlockPlayerInputGate()
    {
        ItemPopupUI.ShowItem("review_dummy", "Review", "No save or API"); yield return null;
        Assert.That(ItemPopupUI.IsBusy, Is.True);
        Assert.That(DialogueManager.IsDialogueOpen, Is.False);
        Assert.That(InputGate.IsBlocked, Is.True, "PlayerMovement and InteractionFocus both rely on this gate.");
    }
    [UnityTest]
    public IEnumerator ItemPopupMustNotAllowTheNpcInteractionPathToOpenAnotherDialogue()
    {
        ItemPopupUI.ShowItem("review_dummy", "Review", "No save or API"); yield return null;
        Assert.That(ItemPopupUI.IsBusy, Is.True);
        var npc = UnityEngine.Object.FindObjectOfType<NPCInteraction>(); Assert.That(npc, Is.Not.Null);
        npc.Interact();
        Assert.That(DialogueManager.IsDialogueOpen, Is.False, "The focus/E path must not open a second modal under an item popup.");
    }
    [UnityTest]
    public IEnumerator RequestMustUseTheProviderSchemaCapturedWhenItWasSent()
    { yield return VerifyProviderChange(false, false); }

    [UnityTest]
    public IEnumerator EventRequestKeepsItsOriginalProviderSchema()
    { yield return VerifyProviderChange(true, false); }

    [UnityTest]
    public IEnumerator QuotaRetryKeepsOriginalEndpointModelAndBackupAccount()
    { yield return VerifyProviderChange(false, true); }

    private IEnumerator VerifyProviderChange(bool isEvent, bool retry)
    {
        var generator = AiDialogueGenerator.Instance; Assert.That(generator, Is.Not.Null);
        var providerField = typeof(AiDialogueGenerator).GetField("provider", Private);
        var urlField = typeof(AiDialogueGenerator).GetField("apiUrl", Private);
        var keyField = typeof(AiDialogueGenerator).GetField("sessionApiKey", Private);
        var enabledField = typeof(AiDialogueGenerator).GetField("enableAiGeneration", Private);
        var modelField = typeof(AiDialogueGenerator).GetField("model", Private);
        var exhaustedField = typeof(AiDialogueGenerator).GetField("exhaustedApiKey", BindingFlags.Static | BindingFlags.NonPublic);
        var provider = providerField.GetValue(generator); var url = urlField.GetValue(generator);
        var key = keyField.GetValue(generator); var enabled = enabledField.GetValue(generator);
        var model = modelField.GetValue(generator); var exhausted = exhaustedField.GetValue(null);
        string backup = Environment.GetEnvironmentVariable("CUSTOM_AI_API_KEY_BACKUP");
        Environment.SetEnvironmentVariable("CUSTOM_AI_API_KEY_BACKUP", "review-dummy-backup");
        var headers = new System.Collections.Generic.List<string>();
        var bodies = new System.Collections.Generic.List<string>();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var arrived = new ManualResetEventSlim(false); var release = new ManualResetEventSlim(false);
        var server = Task.Run(() => {
          for (int attempt = 0; attempt < (retry ? 2 : 1); attempt++)
          {
            using (var client = listener.AcceptTcpClient())
            using (var stream = client.GetStream())
            {
                client.ReceiveTimeout = 5000; client.SendTimeout = 5000;
                var header = new StringBuilder(); int b;
                while ((b = stream.ReadByte()) >= 0) { header.Append((char)b); if (header.ToString().EndsWith("\r\n\r\n")) break; }
                int remaining = 0;
                foreach (string line in header.ToString().Split('\n')) if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) remaining = int.Parse(line.Substring(15).Trim());
                var buffer = new byte[4096]; var requestBody = new System.IO.MemoryStream();
                while (remaining > 0) { int read = stream.Read(buffer, 0, Math.Min(buffer.Length, remaining)); if (read == 0) break; remaining -= read; requestBody.Write(buffer, 0, read); }
                headers.Add(header.ToString()); bodies.Add(Encoding.UTF8.GetString(requestBody.ToArray())); requestBody.Dispose();
                arrived.Set(); if (!release.Wait(5000)) throw new TimeoutException();
                string content = isEvent ? "{\"lines\":[\"ฉันฟังอยู่ เล่าต่อได้เลย\"],\"choices\":[],\"referencedFactIds\":[]}" :
                    "{\"reply\":\"ฉันฟังอยู่ เล่าต่อได้เลย\",\"referencedFactIds\":[]}";
                string response = "{\"choices\":[{\"message\":{\"content\":\"" + content.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}}]}";
                bool quota = retry && attempt == 0;
                byte[] body = Encoding.UTF8.GetBytes(quota ? "{\"error\":{\"message\":\"daily quota exceeded\",\"code\":\"insufficient_quota\"}}" : response);
                byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 " + (quota ? "429 Too Many Requests" : "200 OK") + "\r\nContent-Type: application/json\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                stream.Write(head, 0, head.Length); stream.Write(body, 0, body.Length); stream.Flush();
            }
          }
        });
        Coroutine routine = null;
        var dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.speakerId = "Alice"; dialogue.speakerName = "Alice"; dialogue.lines.Add("บทสำรอง");
        var ev = ScriptableObject.CreateInstance<MiniEventData>(); ev.npcId = "Alice"; ev.eventId = "mock_request"; ev.dialogue = dialogue;
        try
        {
            providerField.SetValue(generator, AiProviderType.OpenAiCompatible);
            urlField.SetValue(generator, "http://127.0.0.1:" + port + "/chat/completions");
            keyField.SetValue(generator, "review-dummy-not-a-real-key"); enabledField.SetValue(generator, true);
            modelField.SetValue(generator, "review-original-model"); exhaustedField.SetValue(null, null);
            bool complete = false; GeneratedChatReply result = null;
            GeneratedDialogueContent eventResult = null;
            routine = generator.StartCoroutine(isEvent ? generator.Generate(ev, r => { eventResult = r; complete = true; }) :
                generator.GenerateReply("Alice", "Alice", "", "สวัสดี", r => { result = r; complete = true; }));
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!arrived.IsSet && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(arrived.IsSet, Is.True, "Loopback mock must receive the request.");
            providerField.SetValue(generator, AiProviderType.OpenAiResponses);
            urlField.SetValue(generator, "http://127.0.0.1:1/must-not-be-used"); modelField.SetValue(generator, "changed-model");
            keyField.SetValue(generator, "changed-dummy-account");
            Environment.SetEnvironmentVariable("CUSTOM_AI_API_KEY_BACKUP", "changed-dummy-backup");
            if (retry) LogAssert.Expect(LogType.Warning, "โควตาของ API key หลักหมดแล้ว — เปลี่ยนไปใช้ key สำรองจนกว่าจะปิดเกม");
            release.Set();
            while (!complete && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(complete, Is.True); Assert.That(server.IsFaulted, Is.False);
            Assert.That(isEvent ? (object)eventResult : result, Is.Not.Null, "Keep the request's original schema.");
            Assert.That(server.Wait(1000), Is.True);
            Assert.That(headers.Count, Is.EqualTo(retry ? 2 : 1));
            Assert.That(headers[0], Does.Contain("Bearer review-dummy-not-a-real-key"));
            if (retry) { Assert.That(headers[1], Does.Contain("Bearer review-dummy-backup")); Assert.That(bodies[1], Is.EqualTo(bodies[0])); }
            Assert.That(bodies[0], Does.Contain("review-original-model"));
            Assert.That(exhaustedField.GetValue(null), Is.Null, "An old account's quota must not mark the newly selected account exhausted.");
        }
        finally
        {
            release.Set(); listener.Stop(); if (routine != null) generator.StopCoroutine(routine);
            providerField.SetValue(generator, provider); urlField.SetValue(generator, url);
            keyField.SetValue(generator, key); enabledField.SetValue(generator, enabled);
            modelField.SetValue(generator, model); exhaustedField.SetValue(null, exhausted);
            Environment.SetEnvironmentVariable("CUSTOM_AI_API_KEY_BACKUP", backup);
            UnityEngine.Object.Destroy(ev); UnityEngine.Object.Destroy(dialogue);
        }
    }

    static void Key(object owner, KeyCode key) => owner.GetType().GetMethod("HandleKey", Private).Invoke(owner, new object[] { key });
    [UnityTest]
    public IEnumerator Room02OfferingWaitsUntilItemCardAndItsClosingFrameAreOver()
    {
        yield return SceneManager.LoadSceneAsync("Room02"); yield return null;
        RoomTransitionManager.CloseRoomOverlays();
        foreach (var item in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) item.enabled = false;
        GameState.Instance.ResetState(); GameState.Instance.AddItem("tome");
        var npc = UnityEngine.Object.FindObjectOfType<NpcPuzzleInteraction>();
        Assert.That(ItemPopupUI.IsBusy, Is.True); npc.Interact();
        Assert.That(GameState.Instance.HasItem("tome"), Is.True);
        Assert.That(GameState.Instance.HasFlag("sena_offering_given"), Is.False);
        yield return null;
        typeof(ItemPopupUI).GetField("popupOpenedTime", Private).SetValue(ItemPopupUI.Instance, -100f);
        Key(ItemPopupUI.Instance, KeyCode.Return); npc.Interact();
        Assert.That(GameState.Instance.HasItem("tome"), Is.True);
        yield return null; npc.Interact();
        Assert.That(GameState.Instance.HasItem("tome"), Is.False);
        Assert.That(GameState.Instance.HasFlag("sena_offering_given"), Is.True);
    }
    [UnityTest]
    public IEnumerator PopupStopsAnAlreadyMovingPlayerBeforeTheNextPhysicsStep()
    {
        var player = UnityEngine.Object.FindObjectOfType<PlayerMovement>(); var body = player.GetComponent<Rigidbody2D>();
        Vector2 before = body.position;
        typeof(PlayerMovement).GetField("movement", Private).SetValue(player, Vector2.right);
        ItemPopupUI.ShowItem("review_dummy", "Review", "test");
        typeof(PlayerMovement).GetMethod("FixedUpdate", Private).Invoke(player, null);
        Assert.That(player.Movement, Is.EqualTo(Vector2.zero)); Assert.That(body.position, Is.EqualTo(before));
        yield return null;
    }
    [UnityTest]
    public IEnumerator SettingsOwnsKeypadKeysIncludingTheFrameItCloses()
    {
        int success = 0, failure = 0; KeypadLockUI.Show("1234", onSuccess: () => success++, onFailure: _ => failure++);
        AiSettingsPanel.Open(); Assert.That(AiSettingsPanel.IsOpen, Is.True);
        foreach (var key in new[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Backspace, KeyCode.Return, KeyCode.Escape }) Key(KeypadLockUI.Instance, key);
        Assert.That(KeypadLockUI.IsOpen, Is.True);
        Assert.That(typeof(KeypadLockUI).GetField("currentInput", Private).GetValue(KeypadLockUI.Instance), Is.EqualTo(""));
        AiSettingsPanel.Close(); Key(KeypadLockUI.Instance, KeyCode.Escape); Assert.That(KeypadLockUI.IsOpen, Is.True);
        Assert.That(success + failure, Is.Zero); yield return null;
        foreach (var key in new[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Return }) Key(KeypadLockUI.Instance, key);
        Assert.That(success, Is.EqualTo(1)); Assert.That(failure, Is.Zero); Assert.That(KeypadLockUI.IsOpen, Is.False);
    }
    [UnityTest]
    public IEnumerator SettingsOwnsJournalKeysIncludingTheFrameItCloses()
    {
        typeof(JournalUI).GetProperty("IsOpen").SetValue(null, true);
        var journal = UnityEngine.Object.FindObjectOfType<JournalUI>(); AiSettingsPanel.Open();
        Key(journal, KeyCode.J); Key(journal, KeyCode.Escape); Assert.That(JournalUI.IsOpen, Is.True);
        AiSettingsPanel.Close(); Key(journal, KeyCode.Escape); Assert.That(JournalUI.IsOpen, Is.True);
        yield return null; Key(journal, KeyCode.Escape); Assert.That(JournalUI.IsOpen, Is.False);
    }
    [UnityTest]
    public IEnumerator SettingsOwnsPopupKeysAndDismissalCannotInteractInTheSameFrame()
    {
        ItemPopupUI.ShowItem("review_dummy", "Review", "test"); yield return null;
        var popup = ItemPopupUI.Instance; typeof(ItemPopupUI).GetField("popupOpenedTime", Private).SetValue(popup, -100f);
        AiSettingsPanel.Open(); Key(popup, KeyCode.Escape); Key(popup, KeyCode.Return); Assert.That(ItemPopupUI.IsBusy, Is.True);
        AiSettingsPanel.Close(); Key(popup, KeyCode.Escape); Assert.That(ItemPopupUI.IsBusy, Is.True);
        yield return null; Key(popup, KeyCode.E); Assert.That(ItemPopupUI.IsBusy, Is.False); Assert.That(InputGate.IsBlocked, Is.True);
        var npc = UnityEngine.Object.FindObjectOfType<NPCInteraction>(); npc.Interact(); Assert.That(DialogueManager.IsDialogueOpen, Is.False);
        yield return null; Assert.That(InputGate.IsBlocked, Is.False); npc.Interact(); Assert.That(DialogueManager.IsDialogueOpen, Is.True);
    }
}
