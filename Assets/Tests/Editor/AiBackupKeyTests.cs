using NUnit.Framework;

namespace MysteryGame.Tests
{
    /// <summary>
    /// The backup API key may only be used once the primary key's quota is
    /// used up. Every other failure must leave it untouched.
    /// </summary>
    public class AiBackupKeyTests
    {
        private const string Primary = "sk_primary";
        private const string Backup = "sk_backup";

        [Test]
        public void ThePrimaryKeyIsUsedUntilItRunsOut()
        {
            Assert.That(AiDialogueGenerator.ChooseKey(Primary, Backup, null), Is.EqualTo(Primary));
            Assert.That(AiDialogueGenerator.ChooseKey(Primary, Backup, ""), Is.EqualTo(Primary));
        }

        [Test]
        public void TheBackupTakesOverOnlyForTheKeyThatRanOut()
        {
            Assert.That(AiDialogueGenerator.ChooseKey(Primary, Backup, Primary), Is.EqualTo(Backup));
            Assert.That(AiDialogueGenerator.ChooseKey("sk_new_key", Backup, Primary), Is.EqualTo("sk_new_key"),
                        "a new key typed into the settings is used, not the backup");
        }

        [Test]
        public void NoBackupOrTheSameKeyChangesNothing()
        {
            Assert.That(AiDialogueGenerator.ChooseKey(Primary, "", Primary), Is.EqualTo(Primary));
            Assert.That(AiDialogueGenerator.ChooseKey(Primary, Primary, Primary), Is.EqualTo(Primary));
            Assert.That(AiDialogueGenerator.ChooseKey("", Backup, ""), Is.Empty,
                        "no primary key means AI is off, not that the backup is used");
        }

        [TestCase(429, "You exceeded your current quota, please check your plan and billing details. [insufficient_quota]")]
        [TestCase(400, "Budget has been exceeded! Current cost: 5.02, Max budget: 5.0")]
        [TestCase(403, "Insufficient credits")]
        [TestCase(429, "Daily token limit reached")]
        [TestCase(429, "Token quota exceeded for this key")]
        [TestCase(402, "โควตาการใช้งานหมดแล้ว")]
        public void QuotaExhaustionIsRecognised(long status, string message)
        {
            Assert.That(AiProviderDiagnostics.Classify(status, message),
                        Is.EqualTo(AiFailureKind.DailyLimitReached), message);
        }

        [TestCase(401, "Incorrect API key provided")]
        [TestCase(429, "Rate limit reached for gpt-4o-mini on tokens per min (TPM): Limit 30000, Used 29000")]
        [TestCase(429, "Too many requests")]
        [TestCase(400, "This model's maximum context length is 8192 tokens")]
        [TestCase(400, "max_tokens is too large: 5000")]
        [TestCase(404, "Invalid model")]
        [TestCase(500, "Internal server error")]
        [TestCase(0, "Cannot resolve destination host")]
        public void OtherFailuresNeverCountAsQuotaExhaustion(long status, string message)
        {
            Assert.That(AiProviderDiagnostics.Classify(status, message),
                        Is.Not.EqualTo(AiFailureKind.DailyLimitReached), message);
        }
    }
}
