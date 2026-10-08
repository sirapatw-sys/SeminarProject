using System.Collections.Generic;
using System.IO;
using System.Text;
using MysteryGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    /// <summary>
    /// How a typed message moves the relationship. A rude or distancing
    /// message must never raise it, and ordinary or friendly Thai must never
    /// be read as rude.
    ///
    /// ToneCorpus/ holds 1,520 labelled lines (NEG / NEU / POS). They were
    /// written in five rounds: the classifier was tuned on each round and the
    /// next round was written unseen, scoring 63%, 58%, 72% and 62% before
    /// its fixes. Unseen phrasing still mostly lands on Neutral, which is why
    /// the AI's reading comes first and the rules are the safety net.
    /// </summary>
    public class PlayerToneTests
    {
        private static IEnumerable<string[]> Corpus()
        {
            string folder = Path.Combine(Application.dataPath, "Tests", "Editor", "ToneCorpus");
            foreach (string file in Directory.GetFiles(folder, "*.txt"))
            {
                foreach (string line in File.ReadAllLines(file, Encoding.UTF8))
                {
                    string[] parts = line.Split(new[] { '\t' }, 2);
                    if (parts.Length == 2)
                    {
                        yield return new[] { parts[0], parts[1], Path.GetFileName(file) };
                    }
                }
            }
        }

        private static string Label(ToneReading reading)
        {
            return reading.IsNegative ? "NEG" : reading.IsPositive ? "POS" : "NEU";
        }

        [Test]
        public void NothingFriendlyOrNeutralIsReadAsRude()
        {
            List<string> wrong = new List<string>();
            foreach (string[] row in Corpus())
            {
                ToneReading reading = PlayerToneClassifier.Read(row[1]);
                if (row[0] != "NEG" && reading.IsNegative)
                {
                    wrong.Add(row[2] + ": " + row[1] + "  <- " + reading);
                }
            }

            Assert.That(wrong, Is.Empty, "these would cost relationship:\n" + string.Join("\n", wrong));
        }

        [Test]
        public void NothingRudeIsReadAsFriendly()
        {
            List<string> wrong = new List<string>();
            foreach (string[] row in Corpus())
            {
                ToneReading reading = PlayerToneClassifier.Read(row[1]);
                if (row[0] == "NEG" && reading.IsPositive)
                {
                    wrong.Add(row[2] + ": " + row[1] + "  <- " + reading);
                }
            }

            Assert.That(wrong, Is.Empty, "these would earn relationship:\n" + string.Join("\n", wrong));
        }

        [Test]
        public void TheCorpusIsReadCorrectly()
        {
            int total = 0;
            int right = 0;
            List<string> misses = new List<string>();
            foreach (string[] row in Corpus())
            {
                total++;
                string predicted = Label(PlayerToneClassifier.Read(row[1]));
                if (predicted == row[0])
                {
                    right++;
                }
                else
                {
                    misses.Add(row[0] + "->" + predicted + "  " + row[1]);
                }
            }

            Assert.That(total, Is.GreaterThanOrEqualTo(1500));
            Assert.That(right / (double)total, Is.GreaterThanOrEqualTo(0.99),
                        right + "/" + total + "\n" + string.Join("\n", misses));
        }

        // The two lines from the playtest that started this, and the two
        // that lost relationship for asking "เจออะไรบ้างไหม".
        [TestCase("แย่มาก", PlayerTone.Hostile)]
        [TestCase("อย่ามาพูดเหมือนเราสนิทกัน", PlayerTone.Cold)]
        [TestCase("เธออยู่ที่นี่มานานกว่าผมนี่ ระหว่างนั้นเจออะไรบ้างไหม??", PlayerTone.Neutral)]
        [TestCase("เธอมาที่นี่นานกว่าผมแล้วใช่ไหม เจออะไรบ้างไหม ช่วยผมที", PlayerTone.Neutral)]
        public void ThePlaytestLines(string message, PlayerTone expected)
        {
            Assert.That(PlayerToneClassifier.Read(message).Tone, Is.EqualTo(expected), message);
        }

        [Test]
        public void IntentFlagsComeFromTheToneReading()
        {
            Assert.That(PlayerIntentClassifier.Classify("หุบปากไปเลย") & PlayerIntent.Hostile, Is.EqualTo(PlayerIntent.Hostile));
            Assert.That(PlayerIntentClassifier.Classify("ไม่ใช่เรื่องของเธอ") & PlayerIntent.Cold, Is.EqualTo(PlayerIntent.Cold));
            Assert.That(PlayerIntentClassifier.IsNegative(PlayerIntentClassifier.Classify("คิดถึงบ้านจัง")), Is.False);
        }

        // ------------------------------------------------------------ combining with the AI

        private static readonly ToneReading Nothing = new ToneReading { Tone = PlayerTone.Neutral };
        private static readonly ToneReading Rude = new ToneReading { Tone = PlayerTone.Hostile };
        private static readonly ToneReading Distant = new ToneReading { Tone = PlayerTone.Cold };
        private static readonly ToneReading Warm = new ToneReading { Tone = PlayerTone.Kind };

        [Test]
        public void TheAiNumberStaysInsideTheToneItNamed()
        {
            Assert.That(RelationshipTuning.Judge(6, "cold", Nothing), Is.EqualTo(-2));
            Assert.That(RelationshipTuning.Judge(-1, "kind", Nothing), Is.EqualTo(4));
            Assert.That(RelationshipTuning.Judge(5, "neutral", Nothing), Is.EqualTo(1));
            Assert.That(RelationshipTuning.Judge(3, "friendly", Nothing), Is.EqualTo(3));
            Assert.That(RelationshipTuning.Judge(3, null, Nothing), Is.EqualTo(3), "no AI: the number stands");
        }

        [Test]
        public void RudenessTheRulesSeeIsNeverRewarded()
        {
            // AI unsure or neutral: the rules' cap
            Assert.That(RelationshipTuning.Judge(0, "neutral", Rude), Is.EqualTo(-5));
            Assert.That(RelationshipTuning.Judge(0, "neutral", Distant), Is.EqualTo(-2));
            Assert.That(RelationshipTuning.Judge(2, null, Rude), Is.EqualTo(-5));
            // a harsher AI reading stands
            Assert.That(RelationshipTuning.Judge(-12, "hostile", Distant), Is.EqualTo(-12));
            // AI says friendly, rules say rude: they disagree, nothing changes
            Assert.That(RelationshipTuning.Judge(4, "friendly", Rude), Is.EqualTo(0));
        }

        [Test]
        public void KindnessTheRulesSeeGetsAtLeastALittle()
        {
            Assert.That(RelationshipTuning.Judge(0, "neutral", Warm), Is.EqualTo(2));
            Assert.That(RelationshipTuning.Judge(0, null, Warm), Is.EqualTo(2));
            // the AI heard sarcasm the rules missed: the AI stands
            Assert.That(RelationshipTuning.Judge(-6, "cold", Warm), Is.EqualTo(-6));
        }
    }
}
