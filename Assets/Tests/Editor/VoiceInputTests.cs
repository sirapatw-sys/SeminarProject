using System;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    /// <summary>Voice typing: what gets recorded, sent and put back in the message box.</summary>
    public class VoiceInputTests
    {
        [Test]
        public void RecordingIsSentAsSixteenBitMonoWav()
        {
            byte[] wav = SpeechToText.EncodeWav(new[] { 0f, 1f, -1f, 0.5f }, 16000);
            Assert.That(Encoding.ASCII.GetString(wav, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(Encoding.ASCII.GetString(wav, 8, 4), Is.EqualTo("WAVE"));
            Assert.That(BitConverter.ToInt16(wav, 22), Is.EqualTo(1), "mono");
            Assert.That(BitConverter.ToInt32(wav, 24), Is.EqualTo(16000));
            Assert.That(BitConverter.ToInt16(wav, 34), Is.EqualTo(16));
            Assert.That(BitConverter.ToInt32(wav, 40), Is.EqualTo(8), "4 samples x 2 bytes");
            Assert.That(wav.Length, Is.EqualTo(44 + 8));
            Assert.That(BitConverter.ToInt16(wav, 46), Is.EqualTo(32767));
            Assert.That(BitConverter.ToInt16(wav, 48), Is.EqualTo(-32767));
        }

        [Test]
        public void MicrophoneRateIsBroughtDownToSixteenKilohertz()
        {
            Assert.That(SpeechToText.Resample(new float[48000], 48000, 16000).Length, Is.EqualTo(16000));
            float[] same = new float[10];
            Assert.That(SpeechToText.Resample(same, 16000, 16000), Is.SameAs(same));
        }

        [Test]
        public void SilenceIsNotSentToTheProvider()
        {
            Assert.That(SpeechToText.IsSilent(new float[16000]), Is.True);
            Assert.That(SpeechToText.IsSilent(null), Is.True);
            float[] speech = new float[16000];
            for (int i = 0; i < speech.Length; i++) speech[i] = 0.3f * Mathf.Sin(i * 0.17f);
            Assert.That(SpeechToText.IsSilent(speech), Is.False);
        }

        [Test]
        public void ChatRequestCarriesTheClipAsInputAudio()
        {
            string body = SpeechToText.BuildChatBody(SpeechToText.KkuModel, "พูดว่า \"สวัสดี\"\nครับ", "QUJD");
            Assert.That(body, Does.Contain("\"model\":\"" + SpeechToText.KkuModel + "\""));
            Assert.That(body, Does.Contain("{\"type\":\"input_audio\",\"input_audio\":{\"data\":\"QUJD\",\"format\":\"wav\"}}"));
            Assert.That(body, Does.Contain("พูดว่า \\\"สวัสดี\\\"\\nครับ"), "instruction is JSON-escaped");
        }

        [Test]
        public void TranscriptIsReadFromEitherApiShape()
        {
            Assert.That(SpeechToText.ReadChatContent(
                "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"สวัสดี อลิซ\"}}]}"),
                Is.EqualTo("สวัสดี อลิซ"));
            Assert.That(SpeechToText.ReadTranscription("{\"text\":\"hello\"}"), Is.EqualTo("hello"));
            Assert.That(SpeechToText.ReadChatContent("not json"), Is.Null);
        }

        [TestCase("  \"สวัสดี\"  ", "สวัสดี")]
        [TestCase("“เปิดประตูหน่อย”", "เปิดประตูหน่อย")]
        [TestCase("ว่าง", "")]
        [TestCase("(ว่าง)", "")]
        [TestCase("", "")]
        public void TranscriptIsTidiedBeforeItReachesTheMessageBox(string raw, string expected)
        {
            Assert.That(SpeechToText.Clean(raw), Is.EqualTo(expected));
        }

        [Test]
        public void CharacterNamesGoAlongAsSpellingHints()
        {
            var names = SpeechToText.NameHints();
            Assert.That(names, Does.Contain("Rina"));
            Assert.That(names, Does.Contain("รินะ"));
            Assert.That(names, Is.Unique);
        }
    }
}
