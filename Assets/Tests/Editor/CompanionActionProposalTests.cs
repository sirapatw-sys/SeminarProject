using MysteryGame.Knowledge;
using NUnit.Framework;

// The model may propose a companion action only from the menu it was offered.
public class CompanionActionProposalTests
{
    private static NpcKnowledgeContext WithMenu(bool askedForHint = false)
    {
        var context = new NpcKnowledgeContext { PlayerAskedForHint = askedForHint };
        context.CompanionActions.Add(new CompanionActionOption { Id = CompanionActionOption.Follow, Description = "เดินตาม" });
        context.CompanionActions.Add(new CompanionActionOption { Id = CompanionActionOption.HelpPrefix + "box", Description = "ช่วยจับกล่อง" });
        return context;
    }

    [Test]
    public void MenuIdsIncludeNoneAndDisappearForHintRequests()
    {
        Assert.That(AiDialogueGenerator.CompanionActionIds(WithMenu()), Is.EqualTo(new[] { "follow", "help:box", "none" }));
        Assert.That(AiDialogueGenerator.CompanionActionIds(WithMenu(true)), Is.Null);
        Assert.That(AiDialogueGenerator.CompanionActionIds(new NpcKnowledgeContext()), Is.Null);
        Assert.That(WithMenu(true).ToCompanionActionSection(), Is.Empty);
        Assert.That(WithMenu().ToCompanionActionSection(), Does.Contain("help:box").And.Contain(CompanionActionOption.None));
    }

    [Test]
    public void StrictSchemaAsksForTheFieldOnlyWhenAMenuExists()
    {
        string plain = AiDialogueGenerator.ReplySchemaJson(null);
        Assert.That(plain, Does.Not.Contain("companionAction"));
        string offered = AiDialogueGenerator.ReplySchemaJson(AiDialogueGenerator.CompanionActionIds(WithMenu()));
        Assert.That(offered, Does.Contain("\"companionAction\":{\"type\":\"string\",\"enum\":[\"follow\",\"help:box\",\"none\"]}"));
        Assert.That(offered, Does.Contain(",\"companionAction\"]"), "Strict schemas require every property.");
        Assert.That(AiDialogueGenerator.CompatibleReplyFormat(null), Does.Not.Contain("companionAction"));
        Assert.That(AiDialogueGenerator.CompatibleReplyFormat(new[] { "follow", "none" }), Does.Contain("follow, none"));
    }

    [Test]
    public void ParserReadsTheProposalWithoutRequiringIt()
    {
        var withAction = AiResponseParser.ParseReplyText(
            "{\"reply\":\"ได้\",\"relationshipDelta\":0,\"referencedFactIds\":[],\"emotion\":\"\",\"playerTone\":\"neutral\",\"companionAction\":\"follow\"}");
        Assert.That(withAction.companionAction, Is.EqualTo("follow"));
        var without = AiResponseParser.ParseReplyText("{\"reply\":\"ได้\",\"relationshipDelta\":0,\"referencedFactIds\":[]}");
        Assert.That(without, Is.Not.Null);
        Assert.That(string.IsNullOrEmpty(without.companionAction), Is.True);
    }

    [Test]
    public void RequestTextEscapesEveryControlCharacter()
    {
        Assert.That(AiDialogueGenerator.EscapeJson("a\"b\\c\nd\u0001e" + (char)0x2028), Is.EqualTo("a\\\"b\\\\c\\nd\\u0001e\\u2028"));
    }
}
