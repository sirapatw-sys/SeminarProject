using System;
using MysteryGame.Companions;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    // Regressions promoted from the review, including real task/social contrasts.
    public class CompanionQualifierRegressionTests : GameStateFixture
    {
        [TestCase("เธอตามเรามา, ใช่ไหม", CompanionCommand.None)]
        [TestCase("เธอช่วยเปิดกล่องสองมือไปแล้ว, ใช่ไหม", CompanionCommand.None)]
        [TestCase("เธอช่วยเปิดกล่องสองมือไปแล้ว, จริงไหม", CompanionCommand.None)]
        [TestCase("ตามเรามาหน่อย, ไม่ต้องแล้ว", CompanionCommand.None)]
        [TestCase("ช่วยเปิดกล่องสองมือหน่อย, ไม่เอาแล้ว", CompanionCommand.None)]
        [TestCase("Follow me, never mind", CompanionCommand.None)]
        [TestCase("เธอตามเรามาอยู่รึเปล่า", CompanionCommand.None)]
        [TestCase("เธอตามเรามาใช่มั้ย", CompanionCommand.None)]
        [TestCase("ช่วยเปิดกล่องสองมือได้ไหม", CompanionCommand.Help)]
        [TestCase("เธอตามเรามาเหรอ, รอตรงนี้ก่อน", CompanionCommand.Wait)]
        [TestCase("＇ช่วยเปิดกล่องสองมือ, ตามเรามาหน่อย＇", CompanionCommand.None)]
        [TestCase("'ช่วยเปิดกล่องสองมือ, ตามเรามาหน่อย'\u200b", CompanionCommand.None)]
        [TestCase("'ช่วยเปิดกล่องสองมือ, ตามเรามาหน่อย'", CompanionCommand.None)]
        [TestCase("ตามเรามาหน่อย, ไม่ต้องแล้ว, รอตรงนี้ก่อน", CompanionCommand.Wait)]
        [TestCase("ตามเรามาหน่อย, รอตรงนี้ก่อน, ไม่เอาแล้ว", CompanionCommand.None)]
        [TestCase("ตามเรามาหน่อย, รอตรงนี้ก่อน, ไม่เอาแล้ว, ช่วยเปิดกล่องสองมือ", CompanionCommand.Help)]
        [TestCase("ตามเรามาหน่อย, เธอช่วยเปิดกล่องสองมือไปแล้ว, ใช่ไหม", CompanionCommand.Follow)]
        [TestCase("ตามเรามาหน่อย, ใช่ไหม, รอตรงนี้ก่อน", CompanionCommand.Wait)]
        [TestCase("ช่วยเปิดกล่องสองมือแล้วตามมา, ใช่ไหม", CompanionCommand.None)]
        [TestCase("ตามเรามาหน่อย, ไม่ต้องแล้วนะครับ", CompanionCommand.None)]
        [TestCase("เธอช่วยเปิดกล่องสองมือไปแล้ว, ใช่หรือเปล่า", CompanionCommand.None)]
        [TestCase("เธอตามเรามา, ใช่ไหมนะครับ", CompanionCommand.None)]
        [TestCase("เธอตามเรามา, right?", CompanionCommand.None)]
        [TestCase("Follow me, aren't you?", CompanionCommand.None)]
        [TestCase("Can you follow me, please?", CompanionCommand.Follow)]
        [TestCase("ช่วยเปิดกล่องสองมือ, ได้ไหม", CompanionCommand.Help)]
        [TestCase("รอตรงนี้ก่อน, เธอตามเรามาใช่ไหม", CompanionCommand.Wait)]
        [TestCase("＂ตามเรามา, รอตรงนี้＂", CompanionCommand.None)]
        [TestCase("＇please follow me, wait here＇", CompanionCommand.None)]
        public void QualifiersAndQuotesMustPreserveTheSpeechAct(string message, CompanionCommand expected)
        {
            CompanionStationData station;
            var actual = CompanionDialogueCommands.Parse(message,
                KnowledgeLibrary.GetRoom("Room01").companionScenario, out station);
            Debug.Log("QUALIFIER_REGRESSION parse " + message + " => " + actual + "; expected=" + expected);
            Assert.That(actual, Is.EqualTo(expected), message);
            if (expected == CompanionCommand.Help) Assert.That(station, Is.Not.Null);
            else Assert.That(station, Is.Null);
            Assert.That(State.HasFlag("companion.room01_alice.two_hands_box.completed"), Is.False);
            Assert.That(State.HasItem("key"), Is.False);
        }

        [TestCase("งานเสร็จแล้วค่ะ", false)]
        [TestCase("ตอนนี้เสร็จแล้วค่ะ", false)]
        [TestCase("เราสำเร็จแล้ว", false)]
        [TestCase("ฉันช่วยแก้ให้เสร็จแล้ว", false)]
        [TestCase("ฉันช่วยเธอทำจนเสร็จแล้ว", false)]
        [TestCase("ฉันเปิดให้แล้ว", false)]
        [TestCase("ฉันจัดการให้เรียบร้อยแล้วค่ะ", false)]
        [TestCase("We're done talking for today.", true)]
        [TestCase("I'm holding back tears.", true)]
        [TestCase("We have finally finished talking for today.", true)]
        [TestCase("เธอทำให้ฉันหายกังวลแล้ว", true)]
        [TestCase("I have completed it.", false)]
        [TestCase("It's already done.", false)]
        [TestCase("I have already opened up to you.", true)]
        [TestCase("We're done chatting.", true)]
        [TestCase("We are done talking.", true)]
        [TestCase("I'm holding back my tears.", true)]
        [TestCase("I have held back tears.", true)]
        [TestCase("I'm holding back tears. It has already been opened.", false)]
        [TestCase("We're done talking. I opened it for you.", false)]
        [TestCase("I'm holding back tears, the code is 4592", false)]
        [TestCase("We're done talking about the box.", false)]
        [TestCase("I'm holding back the box.", false)]
        [TestCase("I'm holding it back.", false)]
        [TestCase("We are done opening it.", false)]
        [TestCase("I have held it for you.", false)]
        [TestCase("ฉันช่วยให้เธอทำใจได้สำเร็จแล้ว", true)]
        [TestCase("เราทำใจได้สำเร็จแล้ว", true)]
        [TestCase("ขอบใจนะ งานเสร็จแล้วค่ะ", false)]
        [TestCase("กิจกรรมนี้เรียบร้อยแล้ว", false)]
        [TestCase("ในที่สุดเราสำเร็จแล้ว", false)]
        [TestCase("ฉันช่วยแก้ไขให้เสร็จแล้ว", false)]
        [TestCase("ฉันช่วยให้เธอสงบลงแล้ว แต่ฉันช่วยแก้ให้เสร็จแล้ว", false)]
        public void ClaimsNeedEvidenceButSocialSpeechMustRemainUsable(string text, bool expected)
        {
            var context = Build("Alice", "Room01", false);
            GeneratedChatReply grounded; string chatReason, eventReason;
            bool chat = NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply {
                reply = text, playerTone = "neutral", referencedFactIds = Array.Empty<string>()
            }, out grounded, out chatReason);
            bool miniEvent = NpcReplyPolicy.ValidateEvent(context, new GeneratedDialogueContent {
                lines = new[] { text }, referencedFactIds = Array.Empty<string>()
            }, out eventReason);
            Debug.Log("QUALIFIER_REGRESSION policy " + text + " => chat=" + chat + "; event=" + miniEvent +
                "; expected=" + expected + "; reason=" + chatReason + " / " + eventReason);
            Assert.That(chat, Is.EqualTo(expected), "chat: " + chatReason);
            Assert.That(miniEvent, Is.EqualTo(expected), "event: " + eventReason);
        }

        [TestCase("งานเสร็จแล้วค่ะ", false)]
        [TestCase("ฉันช่วยแก้ให้เสร็จแล้ว", false)]
        [TestCase("We're done talking for today.", true)]
        [TestCase("I'm holding back tears.", true)]
        [TestCase("I'm holding back tears. It has already been opened.", false)]
        public void CanonDoesNotLicenseUnverifiedClaimsInProseOrChoiceResponses(string prose, bool accepted)
        {
            const string id = "companion.room01_alice.two_hands_box.pending";
            string token = "{fact:" + id + "}";
            var context = Build("Alice", "Room01", false);
            GeneratedChatReply grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply {
                reply = token + "\n" + prose, referencedFactIds = new[] { id }
            }, out grounded, out reason), Is.EqualTo(accepted), reason);
            Assert.That(NpcReplyPolicy.ValidateEvent(context, new GeneratedDialogueContent {
                lines = new[] { token }, referencedFactIds = new[] { id },
                choices = new[] { new GeneratedDialogueChoice { optionText = "ตกลง", responseText = prose } }
            }, out reason), Is.EqualTo(accepted), reason);
            Assert.That(State.HasFlag("companion.room01_alice.two_hands_box.completed"), Is.False);
            Assert.That(State.HasItem("key"), Is.False);
        }
    }
}
