using System;
using System.Collections.Generic;
using System.Linq;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MysteryGame.Companions
{
    /// <summary>Optional in-room chat. Typing blocks player controls, never companion simulation.</summary>
    public class CompanionChatPanel : MonoBehaviour
    {
        public static bool IsOpen => openPanel != null && openPanel.open;
        static CompanionChatPanel openPanel;
        public const KeyCode ToggleKey = KeyCode.C;
        NpcCompanionController actor;
        GameObject canvas;
        TMP_InputField input;
        TMP_Text transcript, status;
        ScrollRect historyScroll;
        readonly Queue<string> lines = new Queue<string>();
        readonly CompanionWorldSession session = new CompanionWorldSession();
        AiRequestOperation operation;
        GameState boundState;
        int serial, sceneRevision;
        bool open, pending;
        float deadline;
        public string LastReply { get; private set; }
        public bool RequestPending => pending;

        void Awake() { actor = GetComponent<NpcCompanionController>(); }
        void OnEnable()
        {
            if (actor == null) actor = GetComponent<NpcCompanionController>();
            actor.WorldTaskFinished += OnResult;
            boundState = GameState.Instance;
            if (boundState != null) { boundState.StateReset += ResetSession; boundState.StateRestored += ResetSession; }
        }
        void Update()
        {
            if (open && (boundState != GameState.Instance || sceneRevision != boundState.SceneRevision || !actor.CanReceiveRoomCommand)) { Close(); return; }
            if (pending && Time.unscaledTime >= deadline) { CancelRequest(); Say("ตอนนี้ AI ยังไม่ตอบ ลองบอกชื่อสิ่งของกับสิ่งที่อยากให้ทำสั้น ๆ อีกครั้งได้นะ"); }
            if (Input.GetKeyDown(ToggleKey) && !open && !InputGate.IsBlocked) OpenSession();
            else if (Input.GetKeyDown(KeyCode.Escape) && open) Close();
        }
        public bool OpenSession()
        {
            if (!actor.CanReceiveRoomCommand || InputGate.IsBlocked && !open) return false;
            if (openPanel != null && openPanel != this) openPanel.Close();
            if (canvas == null) Build();
            boundState = GameState.Instance; sceneRevision = boundState.SceneRevision;
            session.Clear(); open = true; openPanel = this; canvas.SetActive(true);
            input.ActivateInputField(); return true;
        }
        public void Close()
        {
            CancelRequest(); session.Clear(); open = false;
            if (openPanel == this) openPanel = null;
            if (canvas != null) canvas.SetActive(false);
        }
        void ResetSession() { Close(); lines.Clear(); LastReply = null; if (transcript != null) transcript.text = ""; }
        public void Submit(string message)
        {
            message = (message ?? "").Trim();
            if (!open || !actor.CanReceiveRoomCommand || message.Length == 0) return;
            if (message.Length > 500) { Say("ข้อความยาวเกินไป ลองบอกงานที่อยากให้ทำทีละอย่างนะ"); return; }
            CancelRequest(); // The newest player message supersedes any pending interpretation.
            AddLine("คุณ: " + message);
            GeneratedChatReply local;
            if (TryRead(message)) return;
            if (CompanionNaturalRequests.IsJoinRequest(message) && actor.ApplyRoomCommand(CompanionCommand.Follow))
            { Say(actor.Bubble); return; }
            if (CompanionWorldCommands.TryLocal(actor, session, message, out local)) { Say(local.reply); return; }
            CompanionStationData station;
            var command = CompanionDialogueCommands.Parse(message, actor.Scenario, out station);
            if (command != CompanionCommand.None && command != CompanionCommand.Clarify &&
                !CompanionDialogueCommands.RulesVeto(message, command) && actor.ApplyRoomCommand(command, station)) { Say(actor.Bubble); return; }
            var provider = DialogueProviders.Current;
            if (provider == null || !provider.CanGenerate) { Say("ตอนนี้ใช้ AI ไม่ได้ ลองบอกว่าให้ไปหรือตรวจสิ่งไหน พร้อมชื่อสิ่งนั้นนะ"); return; }
            pending = true; deadline = Time.unscaledTime + 30f;
            int request = ++serial, task = actor.TaskRevision, scene = boundState.SceneRevision;
            var capturedState = boundState;
            status.text = "กำลังฟัง… พิมพ์หยุดหรือส่งคำสั่งใหม่ได้";
            operation = new AiRequestOperation(this);
            operation.Start(() => provider.GenerateReply(actor.Scenario.npcId, actor.Scenario.displayName,
                "ช่องคุยระหว่างเล่น: เลือก companionAction ตามคำขอจริง หากกำกวมให้ถามกลับ ห้ามอ้างว่าทำสำเร็จก่อนเกมยืนยัน\n" +
                string.Join("\n", lines), message, reply => {
                    if (!pending || request != serial || !open || capturedState != GameState.Instance ||
                        scene != capturedState.SceneRevision || task != actor.TaskRevision || !actor.CanReceiveRoomCommand) return;
                    CancelRequest();
                    GeneratedChatReply applied;
                    if (reply != null && CompanionWorldCommands.TryProposal(actor, session, message, reply.companionAction, out applied))
                    { Say(applied.reply); return; }
                    if ((reply?.companionAction == CompanionActionOption.Follow || reply?.companionAction == CompanionActionOption.InspectTogether) &&
                        CompanionNaturalRequests.CanProposeJoin(message, KnowledgeLibrary.GetRoom(actor.RoomId)) &&
                        actor.ApplyRoomCommand(CompanionCommand.Follow)) { Say(actor.Bubble); return; }
                    if (reply?.companionAction != null && reply.companionAction.StartsWith("world:", StringComparison.Ordinal))
                    { Say("ฉันยังทำตามคำขอนั้นไม่ได้ ลองบอกสิ่งที่อยากให้ทำกับชื่อเป้าหมายอีกครั้งนะ"); return; }
                    var knowledge = AiDialogueGenerator.BuildKnowledgeContext(actor.Scenario.npcId, message);
                    GeneratedChatReply grounded;
                    string reason;
                    var authored = NpcReplyPolicy.CompanionReply(knowledge) ?? NpcReplyPolicy.AnswerReply(knowledge, message, capturedState) ?? NpcReplyPolicy.HintReply(knowledge);
                    if (authored != null) Say(authored.reply);
                    else if (reply != null && NpcReplyPolicy.TryGroundReply(knowledge, reply, out grounded, out reason)) Say(grounded.reply);
                    else Say("ฉันยังไม่แน่ใจว่าหมายถึงอะไร ลองบอกสิ่งที่อยากให้ทำกับชื่อสิ่งของอีกครั้งนะ");
                }, KnowledgeLibrary.GetNpc(actor.Scenario.npcId)?.persona),
                error => { if (request == serial) { CancelRequest(); Say("ตอนนี้ติดต่อ AI ไม่ได้ ลองส่งคำสั่งอีกครั้งนะ"); } },
                () => { if (request == serial && pending) { CancelRequest(); Say("AI ยังไม่ได้ส่งคำตอบ ลองใหม่อีกครั้งนะ"); } });
        }
        bool TryRead(string message)
        {
            if (CompanionDialogueCommands.RulesVeto(message, CompanionCommand.Help) ||
                !CompanionNaturalRequests.IsEvidenceRequest(message, KnowledgeLibrary.GetRoom(actor.RoomId))) return false;
            var available = EvidenceSharing.Available(boundState, actor.Scenario.npcId);
            var matches = available.Where(f => new[] { f.EvidenceTitle, f.factId }.Concat(f.evidenceAliases ?? new List<string>())
                .Any(n => !string.IsNullOrWhiteSpace(n) && message.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            if (matches.Count != 1) { Say("อยากให้อ่านหลักฐานชิ้นไหน? บอกชื่อหลักฐานที่พบแล้วให้ฉันหน่อยนะ"); return true; }
            EvidenceShareResult shared;
            if (EvidenceSharing.TryShare(boundState, actor.Scenario.npcId, matches[0].factId, out shared, false)) Say(shared.Fact.statement);
            else Say("ฉันยังอ่านหลักฐานชิ้นนั้นไม่ได้");
            return true;
        }
        void OnResult(CompanionTaskResult result)
        {
            // Keep the verified outcome for reopening chat, without popping open a modal.
            Say(result.Message);
        }
        void CancelRequest()
        { ++serial; pending = false; var old = operation; operation = null; old?.Cancel(); if (status != null) status.text = "Enter ส่ง · Esc ปิด · NPC ทำงานต่อระหว่างคุย"; }
        void Say(string text)
        {
            LastReply = text; AddLine(actor.Scenario.displayName + ": " + text);
            if (open && input != null) input.ActivateInputField();
        }
        void AddLine(string text)
        {
            lines.Enqueue(text); while (lines.Count > 6) lines.Dequeue();
            if (transcript != null) transcript.text = string.Join("\n\n", lines);
            if (historyScroll != null) { Canvas.ForceUpdateCanvases(); historyScroll.verticalNormalizedPosition = 0f; }
        }
        void Build()
        {
            canvas = new GameObject("CompanionChatCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 80;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
            var panel = new GameObject("Chat", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(canvas.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
            rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(18, 18); rect.sizeDelta = new Vector2(550, 310);
            panel.GetComponent<Image>().color = new Color(0.035f, 0.075f, 0.11f, 0.97f);
            var font = FindObjectsOfType<TMP_Text>(true).Select(t => t.font).FirstOrDefault(f => f != null);
            Text(panel.transform, "คุยกับ " + actor.Scenario.displayName + " · C เปิดคุยระหว่างเล่น", new Vector2(16, 272), new Vector2(518, 28), 19, font);
            var history = new GameObject("History", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            history.transform.SetParent(panel.transform, false);
            var hr = history.GetComponent<RectTransform>(); hr.anchorMin = hr.anchorMax = Vector2.zero; hr.pivot = Vector2.zero;
            hr.anchoredPosition = new Vector2(16, 88); hr.sizeDelta = new Vector2(518, 178); history.GetComponent<Image>().color = Color.clear;
            transcript = Text(history.transform, string.Join("\n\n", lines), Vector2.zero, new Vector2(518, 0), 18, font);
            transcript.rectTransform.anchorMin = new Vector2(0, 1); transcript.rectTransform.anchorMax = new Vector2(1, 1);
            transcript.rectTransform.pivot = new Vector2(0.5f, 1); transcript.rectTransform.sizeDelta = Vector2.zero;
            transcript.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            historyScroll = history.GetComponent<ScrollRect>(); historyScroll.viewport = hr; historyScroll.content = transcript.rectTransform;
            historyScroll.horizontal = false; historyScroll.movementType = ScrollRect.MovementType.Clamped; historyScroll.scrollSensitivity = 24f;
            var field = new GameObject("Message", typeof(RectTransform), typeof(Image), typeof(TMP_InputField)); field.transform.SetParent(panel.transform, false);
            var fr = field.GetComponent<RectTransform>(); fr.anchorMin = fr.anchorMax = Vector2.zero; fr.pivot = Vector2.zero;
            fr.anchoredPosition = new Vector2(16, 38); fr.sizeDelta = new Vector2(518, 42);
            field.GetComponent<Image>().color = new Color(0.12f, 0.19f, 0.25f);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)); viewport.transform.SetParent(field.transform, false);
            var vr = viewport.GetComponent<RectTransform>(); vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = new Vector2(10, 4); vr.offsetMax = new Vector2(-10, -4);
            var value = Text(viewport.transform, "", Vector2.zero, Vector2.zero, 20, font);
            var tr = value.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            input = field.GetComponent<TMP_InputField>(); input.textViewport = vr; input.textComponent = value;
            input.characterLimit = 500; input.lineType = TMP_InputField.LineType.SingleLine;
            input.onSubmit.AddListener(valueText => { input.text = ""; Submit(valueText); input.ActivateInputField(); });
            status = Text(panel.transform, "Enter ส่ง · Esc ปิด · NPC ทำงานต่อระหว่างคุย", new Vector2(16, 8), new Vector2(518, 24), 15, font);
            Canvas.ForceUpdateCanvases(); historyScroll.verticalNormalizedPosition = 0f;
        }
        static TMP_Text Text(Transform parent, string value, Vector2 position, Vector2 size, float points, TMP_FontAsset font)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>(); t.font = font; t.fontSize = points; t.color = new Color(0.95f, 0.97f, 1f);
            t.text = value; t.richText = false; t.raycastTarget = false;
            var r = t.rectTransform; r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = Vector2.zero; r.anchoredPosition = position; r.sizeDelta = size;
            return t;
        }
        void OnDisable()
        {
            Close(); if (actor != null) actor.WorldTaskFinished -= OnResult;
            if (boundState != null) { boundState.StateReset -= ResetSession; boundState.StateRestored -= ResetSession; }
            lines.Clear(); LastReply = null;
        }
    }
}
