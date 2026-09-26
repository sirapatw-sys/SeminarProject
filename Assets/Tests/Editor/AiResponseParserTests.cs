using System.Collections.Generic;
using NUnit.Framework;

namespace MysteryGame.Tests
{
    /// <summary>
    /// When the provider fails or answers with garbage, parsing must return
    /// null (so the offline reply takes over) and must never throw: an
    /// exception inside the request coroutine would skip the callback and
    /// leave the chat box stuck on "thinking".
    /// </summary>
    public class AiResponseParserTests
    {
        private static string Compatible(string content)
        {
            return "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":" +
                   Quote(content) + "}}]}";
        }

        private static string Quote(string s)
        {
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
        }

        // ------------------------------------------------------------ failures

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json at all")]
        [TestCase("<html><body>502 Bad Gateway</body></html>")]
        [TestCase("{\"choices\":[{\"message\":{\"content\":\"{\\\"reply\\\":\\\"cut off")]
        [TestCase("{\"error\":\"Invalid model\"}")]
        [TestCase("{\"choices\":[]}")]
        [TestCase("{\"choices\":[{\"message\":null}]}")]
        public void BrokenBodiesGiveNullInsteadOfThrowing(string body)
        {
            Assert.DoesNotThrow(() => AiResponseParser.ParseReply(body, false));
            Assert.DoesNotThrow(() => AiResponseParser.ParseReply(body, true));
            Assert.DoesNotThrow(() => AiResponseParser.ParseDialogue(body, false, 2));
            Assert.DoesNotThrow(() => AiResponseParser.ParseDialogue(body, true, 2));

            Assert.That(AiResponseParser.ParseReply(body, false), Is.Null);
            Assert.That(AiResponseParser.ParseReply(body, true), Is.Null);
            Assert.That(AiResponseParser.ParseDialogue(body, false, 2), Is.Null);
        }

        [Test]
        public void MalformedJsonInsideAValidEnvelopeIsRejected()
        {
            string body = Compatible("{\"reply\": \"สวัสดี\", \"relationshipDelta\": ");
            Assert.That(AiResponseParser.ParseReply(body, false), Is.Null);
        }

        [Test]
        public void OutOfRangeRelationshipDeltaIsRejected()
        {
            string body = Compatible("{\"reply\":\"ฮึ\",\"relationshipDelta\":40,\"referencedFactIds\":[]}");
            Assert.That(AiResponseParser.ParseReply(body, false), Is.Null);
        }

        [Test]
        public void EmptyReplyTextIsRejected()
        {
            string body = Compatible("{\"reply\":\"  \",\"relationshipDelta\":0,\"referencedFactIds\":[]}");
            Assert.That(AiResponseParser.ParseReply(body, false), Is.Null);
        }

        [Test]
        public void DialogueWithTheWrongNumberOfChoicesIsRejected()
        {
            string content = "{\"lines\":[\"...\"],\"choices\":[{\"optionText\":\"a\",\"responseText\":\"b\"}]}";
            Assert.That(AiResponseParser.ParseDialogue(Compatible(content), false, 3), Is.Null);
        }

        // ------------------------------------------------------------ successes

        [TestCase("text", "response")]
        [TestCase("player", "npc")]
        [TestCase("optionText", "responseText")]
        public void EventChoicesParseUnderTheNamesModelsActuallyUse(string option, string response)
        {
            // KKU's models named the choice fields themselves when the
            // format was not spelled out; every event then fell back.
            string content = "{\"lines\":[\"นี่ ว่างคุยไหม\"],\"choices\":[" +
                             "{\"" + option + "\":\"ได้สิ\",\"" + response + "\":\"ขอบใจนะ\"}," +
                             "{\"" + option + "\":\"ทำไมล่ะ\",\"" + response + "\":\"ก็แค่คิดเฉยๆ\"}]}";
            GeneratedDialogueContent dialogue = AiResponseParser.ParseDialogue(Compatible(content), false, 2);

            Assert.That(dialogue, Is.Not.Null);
            Assert.That(dialogue.choices[0].optionText, Is.EqualTo("ได้สิ"));
            Assert.That(dialogue.choices[1].responseText, Is.EqualTo("ก็แค่คิดเฉยๆ"));
        }

        [Test]
        public void TheChatRequestSpellsOutTheDialogueShape()
        {
            string format = AiDialogueGenerator.CompatibleDialogueFormat(3);
            Assert.That(format, Does.Contain("\"optionText\"").And.Contain("\"responseText\"").And.Contain("exactly 3 choices"));
        }

        [Test]
        public void CompatibleReplyParses()
        {
            string body = Compatible(
                "{\"reply\":\"...มีอะไรเหรอ?\",\"relationshipDelta\":1," +
                "\"referencedFactIds\":[\"door_locked\"],\"emotion\":\"\"}");
            GeneratedChatReply reply = AiResponseParser.ParseReply(body, false);

            Assert.That(reply, Is.Not.Null);
            Assert.That(reply.reply, Is.EqualTo("...มีอะไรเหรอ?"));
            Assert.That(reply.relationshipDelta, Is.EqualTo(1));
            Assert.That(reply.referencedFactIds, Is.EqualTo(new[] { "door_locked" }));
        }

        [Test]
        public void FencedJsonWithChatterAroundItStillParses()
        {
            string body = Compatible(
                "แน่นอนค่ะ นี่คือคำตอบ:\n```json\n{\"reply\":\"ว้าย!\",\"relationshipDelta\":0," +
                "\"referencedFactIds\":[],\"emotion\":\"shock\"}\n```");
            GeneratedChatReply reply = AiResponseParser.ParseReply(body, false);

            Assert.That(reply, Is.Not.Null);
            Assert.That(reply.emotion, Is.EqualTo("shock"));
        }

        [Test]
        public void ResponsesApiOutputTextParses()
        {
            string inner = "{\"reply\":\"ข้ามิใช่พี่เลี้ยงของเจ้าดอก\",\"relationshipDelta\":-5,\"referencedFactIds\":[],\"emotion\":\"\"}";
            string body = "{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":" + Quote(inner) + "}]}]}";
            GeneratedChatReply reply = AiResponseParser.ParseReply(body, true);

            Assert.That(reply, Is.Not.Null);
            Assert.That(reply.relationshipDelta, Is.EqualTo(-5));
        }

        [Test]
        public void CompatibleDialogueParses()
        {
            string content = "{\"lines\":[\"ขอโทษนะ...\"],\"choices\":[" +
                             "{\"optionText\":\"ได้สิ\",\"responseText\":\"ขอบคุณ\"}," +
                             "{\"optionText\":\"ไม่ว่าง\",\"responseText\":\"อืม\"}]}";
            GeneratedDialogueContent dialogue =
                AiResponseParser.ParseDialogue(Compatible(content), false, 2);

            Assert.That(dialogue, Is.Not.Null);
            Assert.That(dialogue.lines.Length, Is.EqualTo(1));
        }

        // ------------------------------------------------------------ quota

        [Test]
        public void QuotaIsReadFromRateLimitHeaders()
        {
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "X-RateLimit-Remaining-Requests", "42" },
                { "X-RateLimit-Limit-Requests", "100" },
                { "x-ratelimit-remaining-tokens", "9000" },
            };

            string quota = AiProviderDiagnostics.ReadQuota(headers, null);
            Assert.That(quota, Does.Contain("42/100"));
            Assert.That(quota, Does.Contain("9000"));
        }

        [Test]
        public void QuotaFallsBackToABodyField()
        {
            string quota = AiProviderDiagnostics.ReadQuota(
                new Dictionary<string, string>(), "{\"usage\":{},\"remaining_quota\": 17}");
            Assert.That(quota, Does.Contain("17"));
        }

        [Test]
        public void NoQuotaInformationMeansAnEmptyString()
        {
            Assert.That(AiProviderDiagnostics.ReadQuota(null, "{\"id\":\"x\"}"), Is.Empty);
            Assert.That(AiProviderDiagnostics.ReadQuota(new Dictionary<string, string>(), null), Is.Empty);
        }

        [Test]
        public void RedactionHidesKeys()
        {
            string text = AiProviderDiagnostics.Redact("Bearer sk-abcdefghijklmnop failed");
            Assert.That(text, Does.Not.Contain("abcdefghijklmnop"));
        }
    }
}
