using System;
using MysteryGame.Companions;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    // Regressions for speech-act boundaries and grounded companion dialogue.
    public class CompanionSpeechActRegressionTests : GameStateFixture
    {
        [TestCase("เธอตามเรามาเหรอ, รอตรงนี้ก่อน", CompanionCommand.Wait)]
        [TestCase("เธอช่วยเปิดกล่องสองมือแล้วใช่ไหม, รอตรงนี้ก่อน", CompanionCommand.Wait)]
        [TestCase("เธอกำลังตามเรามาใช่ไหม, ไม่ต้องตามแล้ว", CompanionCommand.Wait)]
        [TestCase("เธอตามเรามาอยู่หรือเปล่า", CompanionCommand.None)]
        [TestCase("เธอช่วยเปิดกล่องสองมือไปแล้วรึเปล่า", CompanionCommand.None)]
        [TestCase("เธอช่วยเปิดกล่องสองมืออยู่หรือไม่", CompanionCommand.None)]
        [TestCase("ช่วยเปิดกล่องสองมือได้ไหม", CompanionCommand.Help)]
        [TestCase("เธอช่วยเปิดกล่องสองมือแล้วใช่ไหม", CompanionCommand.None)]
        [TestCase("ช่วยหน่อย, ตามเรามาหน่อย", CompanionCommand.Follow)]
        [TestCase("กล่องสองมือเปิดยังไง, ตามเรามาหน่อย", CompanionCommand.Follow)]
        [TestCase("ช่วยสอนฉันเปิดกล่องสองมือหน่อย, รอตรงนี้ก่อน", CompanionCommand.Wait)]
        [TestCase("How do we open the box? Please follow me.", CompanionCommand.Follow)]
        [TestCase("Are you following me? Wait here please.", CompanionCommand.Wait)]
        [TestCase("เธอตามเรามาเหรอ แต่รอตรงนี้ก่อน", CompanionCommand.Wait)]
        [TestCase("ช่วยหน่อย, ตามเรามา แล้วรอตรงนี้", CompanionCommand.Clarify)]
        [TestCase("ตามเรามา รอตรงนี้ก่อน", CompanionCommand.Clarify)]
        [TestCase("Follow me wait here", CompanionCommand.Clarify)]
        [TestCase("ช่วยเปิดกล่องสองมือ ตามเรามา", CompanionCommand.Clarify)]
        [TestCase("รอตรงนี้ช่วยเปิดกล่องสองมือ", CompanionCommand.Clarify)]
        [TestCase("ตามเรามาช่วยเปิดกล่องสองมือ", CompanionCommand.Clarify)]
        [TestCase("Follow me help me open the box", CompanionCommand.Clarify)]
        [TestCase("ตามเรามา, ตามเรามาหน่อย", CompanionCommand.Follow)]
        [TestCase("Can you open the box and follow me?", CompanionCommand.Clarify)]
        [TestCase("ไม่ต้องช่วยเปิดกล่องสองมือ, ตามเรามาหน่อย", CompanionCommand.Follow)]
        [TestCase("อย่าเดินตามเรา, รอตรงนี้ก่อน", CompanionCommand.Wait)]
        [TestCase("อย่า, ตามเรามา", CompanionCommand.None)]
        [TestCase("Don't, follow me", CompanionCommand.None)]
        [TestCase("Do not follow me and wait here", CompanionCommand.None)]
        [TestCase("Do you follow me and wait here?", CompanionCommand.None)]
        [TestCase("เธอตามเรามาแล้วรอตรงนี้ใช่ไหม", CompanionCommand.None)]
        [TestCase("ถ้าฉันบอกว่า, ตามเรามา", CompanionCommand.None)]
        [TestCase("He said, follow me", CompanionCommand.None)]
        [TestCase("'follow me', wait here", CompanionCommand.None)]
        [TestCase("'Don't follow me', wait here", CompanionCommand.None)]
        [TestCase("เธอพิมพ์ 'follow me'; รอตรงนี้ก่อน", CompanionCommand.None)]
        [TestCase("เพื่อนของฉันตามเรามา", CompanionCommand.None)]
        [TestCase("ฉันเห็นเธอรอตรงนี้", CompanionCommand.None)]
        [TestCase("ช่วยทุบกล่องสองมือหน่อย", CompanionCommand.None)]
        [TestCase("Can you destroy the box?", CompanionCommand.None)]
        [TestCase("Alice please follow me", CompanionCommand.Follow)]
        [TestCase("Alice, please follow me", CompanionCommand.Follow)]
        [TestCase("เธอช่วยเปิดกล่องได้หรือเปล่า", CompanionCommand.Help)]
        [TestCase("ช่วยเปิดกล่องแล้วตามมาได้ไหม", CompanionCommand.Clarify)]
        [TestCase("ไม่ต้องกลัวนะ, เราอยู่ตรงนี้", CompanionCommand.Comfort)]
        public void ReadTheSpeechActOfTheActionClause(string message, CompanionCommand expected)
        {
            CompanionStationData station;
            var actual = CompanionDialogueCommands.Parse(message, KnowledgeLibrary.GetRoom("Room01").companionScenario, out station);
            Debug.Log("REGRESSION parse: " + message + " => " + actual + "; expected=" + expected);
            Assert.That(actual, Is.EqualTo(expected), message);
            if (expected == CompanionCommand.Help) Assert.That(station, Is.Not.Null);
            else Assert.That(station, Is.Null);
        }

        [TestCase("เธอทำให้ฉันหายกังวลแล้ว", true)]
        [TestCase("เธอทำให้ฉันกล้าขึ้นแล้ว", true)]
        [TestCase("เธอทำให้ฉันเชื่อใจได้แล้ว", true)]
        [TestCase("ฉันช่วยให้เธอสงบลงแล้ว", true)]
        [TestCase("เธอทำให้ฉันหายเศร้าแล้ว", true)]
        [TestCase("It has already been opened for you.", false)]
        [TestCase("We are finally done.", false)]
        [TestCase("ฉันจัดการให้เรียบร้อยแล้วค่ะ", false)]
        [TestCase("I have already opened it for you.", false)]
        [TestCase("I just opened it for you.", false)]
        [TestCase("ฉันทำให้เรียบร้อยแล้วค่ะ", false)]
        [TestCase("เธอทำให้ฉันรู้สึกดีขึ้นแล้ว", true)]
        [TestCase("I have already opened up to you.", true)]
        [TestCase("เธอทำให้ฉันหายเครียดแล้ว", true)]
        [TestCase("เธอทำให้ฉันอยากกลับบ้านแล้ว", true)]
        [TestCase("ฉันทำให้ความกลัวเบาลงแล้ว", true)]
        [TestCase("ฉันช่วยให้เธอไม่โดดเดี่ยวแล้ว", true)]
        [TestCase("ฉันทำใจได้สำเร็จแล้ว", true)]
        [TestCase("I have complete confidence in you.", true)]
        [TestCase("I am closer to you now.", true)]
        [TestCase("We have finally finished talking for today.", true)]
        [TestCase("It was already opened for you.", false)]
        [TestCase("It hasn't been opened yet.", false)]
        [TestCase("This has just been completed.", false)]
        [TestCase("It's already done.", false)]
        [TestCase("We're finally done.", false)]
        [TestCase("I have already been following you.", false)]
        [TestCase("I've been following you.", false)]
        [TestCase("We've already been holding it.", false)]
        [TestCase("I’ve just been following you.", false)]
        [TestCase("ฉันจัดการมันให้เธอไปแล้ว", false)]
        [TestCase("ทำงานเสร็จแล้วค่ะ", false)]
        [TestCase("ฉันทำให้แล้ว", false)]
        [TestCase("เธอทำให้ฉันหายกังวลแล้ว รหัสคือ 4592", false)]
        [TestCase("ฉันช่วยให้เธอสงบลงแล้ว แต่ฉันเปิดให้แล้ว", false)]
        [TestCase("I have opened up to you. It has already been opened.", false)]
        [TestCase("We have finally finished talking, the code is 4592", false)]
        public void PreserveSocialSpeechButRequireFactsForActivityReports(string text, bool expected)
        {
            var context = Build("Alice", "Room01", false);
            GeneratedChatReply grounded; string chatReason, eventReason;
            bool chat = NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply {
                reply = text, playerTone = "neutral", referencedFactIds = Array.Empty<string>()
            }, out grounded, out chatReason);
            bool miniEvent = NpcReplyPolicy.ValidateEvent(context, new GeneratedDialogueContent {
                lines = new[] { text }, referencedFactIds = Array.Empty<string>()
            }, out eventReason);
            Debug.Log("REGRESSION policy: " + text + " => chat=" + chat + "; event=" + miniEvent +
                "; expected=" + expected + "; reason=" + chatReason + " / " + eventReason);
            Assert.That(chat, Is.EqualTo(expected), "chat: " + chatReason);
            Assert.That(miniEvent, Is.EqualTo(expected), "event: " + eventReason);
        }

        [TestCase("เธอทำให้ฉันหายกังวลแล้ว", true)]
        [TestCase("I have complete confidence in you.", true)]
        [TestCase("ฉันจัดการให้เรียบร้อยแล้วค่ะ", false)]
        [TestCase("It has already been opened for you.", false)]
        [TestCase("เธอทำให้ฉันเชื่อใจได้แล้ว รหัสคือ 4592", false)]
        public void AValidFactOnAnotherLineDoesNotLicenseInventedActivityProse(string prose, bool accepted)
        {
            const string id = "companion.room01_alice.two_hands_box.pending";
            var context = Build("Alice", "Room01", false);
            string text = prose + "\n{fact:" + id + "}";
            GeneratedChatReply grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply {
                reply = text, referencedFactIds = new[] { id }
            }, out grounded, out reason), Is.EqualTo(accepted), reason);
            Assert.That(NpcReplyPolicy.ValidateEvent(context, new GeneratedDialogueContent {
                lines = new[] { text }, referencedFactIds = new[] { id }
            }, out reason), Is.EqualTo(accepted), reason);
            if (accepted) Assert.That(grounded.reply, Does.Contain(prose).And.Contain("ยังไม่สำเร็จ"));
            Assert.That(State.HasFlag("companion.room01_alice.two_hands_box.completed"), Is.False);
            Assert.That(State.HasItem("key"), Is.False);
        }
    }
}
