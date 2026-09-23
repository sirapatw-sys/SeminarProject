using System.Collections.Generic;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MysteryGame.Tests
{
    /// <summary>
    /// Conversation memory, the data-driven offline replies, secrets, NPC to
    /// NPC relationships and the events that react to them.
    /// </summary>
    public class NpcConversationTests : GameStateFixture
    {
        private static MiniEventData LoadEvent(string name)
        {
            MiniEventData data = AssetDatabase.LoadAssetAtPath<MiniEventData>(
                "Assets/Data/Events/" + name + ".asset");
            Assert.That(data, Is.Not.Null, name);
            return data;
        }

        private static DialogueData LoadDialogue(string name)
        {
            DialogueData data = AssetDatabase.LoadAssetAtPath<DialogueData>(
                "Assets/Data/Dialogue/" + name + ".asset");
            Assert.That(data, Is.Not.Null, name);
            return data;
        }

        private void Choose(DialogueData dialogue, int index)
        {
            foreach (ActionCommand action in dialogue.choices[index].actions)
            {
                action.Execute(State);
            }
        }

        // ------------------------------------------------------------ history

        [Test]
        public void HistoryKeepsTheNewestTurnsWithinBudget()
        {
            List<ConversationTurn> log = new List<ConversationTurn>();
            for (int i = 0; i < 30; i++)
            {
                log.Add(new ConversationTurn { SpeakerId = i % 2 == 0 ? "player" : "Alice", Text = "ข้อความที่ " + i });
            }

            List<ConversationTurn> picked = NpcKnowledgeContext.SelectRecentTurns(log, 10, 10000);
            Assert.That(picked.Count, Is.EqualTo(10));
            Assert.That(picked[0].Text, Is.EqualTo("ข้อความที่ 20"), "oldest first");
            Assert.That(picked[9].Text, Is.EqualTo("ข้อความที่ 29"));

            List<ConversationTurn> tight = NpcKnowledgeContext.SelectRecentTurns(log, 10, 40);
            Assert.That(tight.Count, Is.LessThan(10), "the character budget caps the prompt");
            Assert.That(tight[tight.Count - 1].Text, Is.EqualTo("ข้อความที่ 29"));
        }

        [Test]
        public void AVeryLongTurnIsClippedNotDropped()
        {
            List<ConversationTurn> log = new List<ConversationTurn>
            {
                new ConversationTurn { SpeakerId = "player", Text = new string('ก', 2000) },
            };

            List<ConversationTurn> picked = NpcKnowledgeContext.SelectRecentTurns(log, 10, 1400);
            Assert.That(picked.Count, Is.EqualTo(1));
            Assert.That(picked[0].Text.Length, Is.LessThanOrEqualTo(NpcKnowledgeContext.HistoryTurnCharLimit + 1));
        }

        [Test]
        public void TheConversationLogIsCappedPerNpc()
        {
            for (int i = 0; i < GameState.MaxConversationTurns + 15; i++)
            {
                State.AddConversationTurn("Alice", "player", "line " + i);
            }

            State.AddConversationTurn("Rina", "player", "hello");
            Assert.That(State.GetConversationLog("Alice").Count, Is.EqualTo(GameState.MaxConversationTurns));
            Assert.That(State.GetConversationLog("Rina").Count, Is.EqualTo(1));
        }

        [Test]
        public void ThePromptCarriesSeveralPreviousMessages()
        {
            State.AddConversationTurn("Alice", ConversationTurn.Player, "ชื่ออะไรเหรอ");
            State.AddConversationTurn("Alice", "Alice", "...อลิซ");
            State.AddConversationTurn("Alice", ConversationTurn.Player, "มาจากไหน");
            State.AddConversationTurn("Alice", "Alice", "...จำไม่ได้");

            NpcKnowledgeContext ctx = Build("Alice", "Room01", askedForHint: false);
            string prompt = AiDialogueGenerator.BuildReplyPrompt(
                "Alice", "Alice", "context", "แล้วตอนนี้รู้สึกยังไง", null, ctx);

            Assert.That(prompt, Does.Contain("ชื่ออะไรเหรอ"));
            Assert.That(prompt, Does.Contain("...จำไม่ได้"));
            Assert.That(prompt, Does.Contain("บทสนทนาล่าสุด"));
        }

        [Test]
        public void ThePromptIsBuiltFromTheProfileNotHardCodedText()
        {
            NpcKnowledgeContext ctx = Build("Rina", "Room03", askedForHint: false);
            string prompt = AiDialogueGenerator.BuildReplyPrompt(
                "Rina", "รินะ (Rina)", "context", "สวัสดี", null, ctx);

            Assert.That(prompt, Does.Contain(KnowledgeLibrary.GetNpc("Rina").speechStyle));
            Assert.That(prompt, Does.Contain("ห้องรับแขกร้าง"));
        }

        // ------------------------------------------------------------ offline replies

        [Test]
        public void OfflineRepliesComeFromTheNpcsOwnRules()
        {
            GeneratedChatReply sena = NpcOfflineReplies.Build(Build("Sena", "Room02", false), "หุบปากไปเลย", State);
            Assert.That(sena.reply, Does.Contain("มนุษย์ชั้นต่ำ"));
            Assert.That(sena.relationshipDelta, Is.LessThan(0));

            GeneratedChatReply alice = NpcOfflineReplies.Build(Build("Alice", "Room01", false), "สวัสดี", State);
            Assert.That(alice.reply, Does.Contain("สวัสดี"));
        }

        [Test]
        public void AHintRequestFallsThroughToTheRoomLadder()
        {
            NpcKnowledgeContext ctx = Build("Rina", "Room03", askedForHint: true);
            GeneratedChatReply reply = NpcOfflineReplies.Build(ctx, "ขอคำใบ้หน่อย ต้องทำอะไรต่อ", State);

            Assert.That(reply.reply, Is.EqualTo(ctx.DeterministicHint));
        }

        [Test]
        public void StelleIsShyAndShowsHerFeelingsFromTheFirstTalk()
        {
            NpcProfileData profile = KnowledgeLibrary.GetNpc("Stelle");
            foreach (string message in new[] { "กลัวไหม", "สวัสดี", "ชื่ออะไร", "อืม" })
            {
                GeneratedChatReply reply = NpcOfflineReplies.Build(Build("Stelle", "Room03", false), message, State);
                Assert.That(reply.emotion, Is.Not.Empty, message);
                Assert.That(profile.FindEmotion(reply.emotion), Is.Not.Null,
                            "every emotion a rule uses needs a face in the emotion box");
            }

            GeneratedChatReply greeting = NpcOfflineReplies.Build(Build("Stelle", "Room03", false), "สวัสดี", State);
            Assert.That(greeting.reply, Does.Contain("สะ...สวัสดี"), "she stammers before she warms up");

            State.ChangeRelationship("Stelle", 20);   // 35 -> 55, warmed up
            GeneratedChatReply friend = NpcOfflineReplies.Build(Build("Stelle", "Room03", false), "สวัสดี", State);
            Assert.That(friend.reply, Does.Not.Contain("สะ...สวัสดี"));
        }

        [Test]
        public void SenasRiddleAnswerIsOnlyPraisedAfterTheOffering()
        {
            GeneratedChatReply early = NpcOfflineReplies.Build(Build("Sena", "Room02", false), "พรุ่งนี้", State);
            Assert.That(early.reply, Does.Not.Contain("ตอบถูก"));

            State.SetFlag("sena_offering_given");
            GeneratedChatReply late = NpcOfflineReplies.Build(Build("Sena", "Room02", false), "พรุ่งนี้", State);
            Assert.That(late.reply, Does.Contain("ตอบถูก"));
        }

        [TestCase("พรุ่งนี้")]
        [TestCase("วันพรุ่งนี้")]
        [TestCase("วันถัดไป")]
        [TestCase("วันต่อไป")]
        [TestCase("วันรุ่งขึ้น")]
        [TestCase("Tomorrow")]
        [TestCase("the next day")]
        public void SenaAcceptsEveryWayOfSayingTomorrow(string answer)
        {
            Assert.That(SenaInteraction.IsCorrectRiddleAnswer(answer), Is.True, answer);

            // and what she says offline agrees with what the gate does
            State.SetFlag("sena_offering_given");
            GeneratedChatReply reply = NpcOfflineReplies.Build(Build("Sena", "Room02", false), answer, State);
            Assert.That(reply.reply, Does.Contain("ตอบถูก"), answer);
        }

        [Test]
        public void SpacesInsideTheAnswerDoNotMatter()
        {
            Assert.That(SenaInteraction.IsCorrectRiddleAnswer("วัน ถัด ไป"), Is.True);
            Assert.That(SenaInteraction.IsCorrectRiddleAnswer("พรุ่ง นี้"), Is.True);
        }

        [TestCase("หนังสือ")]
        [TestCase("เมื่อวาน")]
        [TestCase("")]
        public void SenaRejectsWrongAnswers(string answer)
        {
            Assert.That(SenaInteraction.IsCorrectRiddleAnswer(answer), Is.False, answer);
        }

        [Test]
        public void SenasPromptListsExactlyTheAnswersTheGateAccepts()
        {
            string notes = string.Join("\n", KnowledgeLibrary.GetNpc("Sena").situationalNotes.ConvertAll(n => n.note));
            foreach (string answer in new[] { "พรุ่งนี้", "วันถัดไป", "วันต่อไป", "วันรุ่งขึ้น", "อนาคต", "Tomorrow" })
            {
                Assert.That(notes, Does.Contain(answer));
            }
        }

        [Test]
        public void Room03StoryBeatsPlayWithoutWaitingForThePlayer()
        {
            foreach (string name in new[] { "Stelle_Scare_Event", "Room03_Quarrel_Event", "Stelle_Relief_Event", "Stelle_Hurt_Event" })
            {
                MiniEventData data = LoadEvent(name);
                Assert.That(data.storyBeat && data.autoStart, Is.True, name);
            }
        }

        [Test]
        public void FirstMeetingChoicesAreNotOfferedAgain()
        {
            foreach (string name in new[] { "Rina_Room03", "Stelle_Room03", "Alice_Room03", "Alice_Intro", "Alice_Room02" })
            {
                Assert.That(LoadDialogue(name).firstMeetingChoicesOnly, Is.True, name);
            }
        }

        [Test]
        public void ReturnGreetingsFollowTheRelationship()
        {
            // Relationship first: changing it afterwards would count as "the
            // room changed since we last talked" and pick that greeting.
            State.ChangeRelationship("Alice", -30);
            State.RecordConversation("Alice");
            Assert.That(NpcOfflineReplies.ReturnGreeting(KnowledgeLibrary.GetNpc("Alice"), State),
                        Does.Contain("ยังไม่ลืม"));
        }

        // ------------------------------------------------------------ secrets

        [Test]
        public void SecretsUnlockWithRelationshipAndStayUnciteableBefore()
        {
            NpcKnowledgeContext before = Build("Rina", "Room03", false);
            string offending;
            Assert.That(before.ValidateReferences(new[] { "rina_fear" }, out offending), Is.False);
            Assert.That(before.ValidateReferences(new[] { "rina_halo" }, out offending), Is.True,
                        "backstory is always shareable");

            State.ChangeRelationship("Rina", 25);   // 45 -> 70
            NpcKnowledgeContext after = Build("Rina", "Room03", false);
            Assert.That(after.ValidateReferences(new[] { "rina_fear" }, out offending), Is.True);
            Assert.That(after.ValidateReferences(new[] { "rina_forgetting" }, out offending), Is.False,
                        "the deeper secret needs 75");
        }

        // ------------------------------------------------------------ NPC relationships and events

        [Test]
        public void NpcBondsStartFromTheAuthoredValues()
        {
            Assert.That(State.GetNpcRelationship("Rina", "Stelle"), Is.EqualTo(38));
            Assert.That(State.GetNpcRelationship("Stelle", "Rina"), Is.EqualTo(38));
            Assert.That(State.GetRelationship("Stelle"), Is.EqualTo(35), "Stelle starts shy with someone she just met");
        }

        [Test]
        public void TheQuarrelWaitsForTheMusicBoxAndHappensOnce()
        {
            State.SetCurrentScene("Room03");
            MiniEventData quarrel = LoadEvent("Room03_Quarrel_Event");
            Assert.That(quarrel.CanTrigger(State), Is.False, "nothing to argue about yet");

            State.SetFlag("saw_mirror_message");
            State.SetFlag("music_box_needs_key");
            Assert.That(quarrel.CanTrigger(State), Is.True);

            State.SetFlag(quarrel.CompletedFlag);
            Assert.That(quarrel.CanTrigger(State), Is.False);
        }

        [Test]
        public void MediatingMendsTheBondAndOpensStelleUp()
        {
            DialogueData quarrel = LoadDialogue("Room03_Quarrel");
            Assert.That(quarrel.choices.Count, Is.EqualTo(4), "listen / mediate / side with Rina / side with Stelle");

            Choose(quarrel, 1);
            Assert.That(State.HasFlag("room03_quarrel_mediated"), Is.True);
            Assert.That(State.GetNpcRelationship("Rina", "Stelle"), Is.GreaterThan(45),
                        "the conflict event must not re-trigger after a mediation");

            NpcKnowledgeContext stelle = Build("Stelle", "Room03", false);
            Assert.That(stelle.ActiveNotes.Exists(n => n.Contains("ไกล่เกลี่ย")), Is.True,
                        "the choice is remembered in how Stelle talks later");
        }

        [Test]
        public void SidingWithRinaHurtsStelleUntilThePlayerApologises()
        {
            State.SetCurrentScene("Room03");
            Choose(LoadDialogue("Room03_Quarrel"), 2);
            Assert.That(State.HasFlag("room03_sided_rina"), Is.True);
            Assert.That(State.GetRelationship("Stelle"), Is.LessThan(35));

            State.SetFlag("room03_door_unlocked");
            State.SetFlag("saw_mirror_message");
            State.SetFlag("music_box_needs_key");
            State.SetFlag("found_winding_key");
            State.SetFlag("ghost_lullaby_played");
            Assert.That(LoadEvent("Stelle_Hurt_Event").CanTrigger(State), Is.True);
            Assert.That(LoadEvent("Stelle_Relief_Event").CanTrigger(State), Is.False);

            Choose(LoadDialogue("Stelle_Hurt"), 0);
            Assert.That(State.HasFlag("stelle_forgave"), Is.True);
        }

        [Test]
        public void StellesScareReactsToTheMirrorNotToAClock()
        {
            State.SetCurrentScene("Room03");
            MiniEventData scare = LoadEvent("Stelle_Scare_Event");
            Assert.That(scare.CanTrigger(State), Is.False);

            State.SetFlag("saw_mirror_message");
            Assert.That(scare.CanTrigger(State), Is.True, "fires the moment the mirror has been read");

            State.SetFlag("music_box_needs_key");
            Assert.That(scare.CanTrigger(State), Is.False, "and is stale once the player has moved on");
        }

        // ------------------------------------------------------------ save / load

        [Test]
        public void ASnapshotRoundTripsThroughJson()
        {
            State.SetCurrentScene("Room03");
            State.SetFlag("saw_mirror_message");
            State.AddItem("winding_key");
            State.ChangeRelationship("Stelle", 10);
            State.ChangeNpcRelationship("Rina", "Stelle", 20);
            State.AddConversationTurn("Stelle", "player", "ไม่ต้องกลัว");
            State.RecordConversation("Stelle");

            string json = JsonUtility.ToJson(State.CreateSnapshot());

            GameObject other = new GameObject("RestoredState");
            GameState restored = other.AddComponent<GameState>();
            try
            {
                restored.RestoreSnapshot(JsonUtility.FromJson<StateSnapshot>(json));
                Assert.That(restored.GetCurrentScene(), Is.EqualTo("Room03"));
                Assert.That(restored.HasFlag("saw_mirror_message"), Is.True);
                Assert.That(restored.HasItem("winding_key"), Is.True);
                Assert.That(restored.GetRelationship("Stelle"), Is.EqualTo(45));
                Assert.That(restored.GetNpcRelationship("Stelle", "Rina"), Is.EqualTo(58));
                Assert.That(restored.GetConversationLog("Stelle")[0].Text, Is.EqualTo("ไม่ต้องกลัว"));
                Assert.That(restored.GetConversationCount("Stelle"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }
    }
}
