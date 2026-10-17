using MysteryGame.Companions;
using NUnit.Framework;

public class CompanionSemanticIntentTests
{
    static readonly string[] Menu = { "world:move:at:desk", "world:move:left:desk", "world:inspect:at:desk", "world:move:at:door", "none" };
    [Test]
    public void SchemaOffersOnlyKnownTargetsAndUsesRequiredClosedNestedObjects()
    {
        string schema = AiDialogueGenerator.ReplySchemaJson(Menu);
        Assert.That(schema, Does.Contain("\"worldIntent\":{\"type\":\"object\""));
        Assert.That(schema, Does.Contain("\"enum\":[\"\",\"desk\",\"door\"]"));
        Assert.That(schema, Does.Contain("\"required\":[\"speechAct\",\"verb\",\"targetId\",\"relation\",\"referenceId\"],\"additionalProperties\":false"));
        Assert.That(schema, Does.Contain("\"companionAction\",\"worldIntent\"]"));
        Assert.That(AiDialogueGenerator.ReplySchemaJson(new[] { "follow", "none" }), Does.Not.Contain("worldIntent"));
        Assert.That(AiDialogueGenerator.CompatibleReplyFormat(Menu), Does.Contain("worldIntent").And.Contain("referenceId"));
    }
    [Test]
    public void ParserRetainsSemanticIntentAndLegacyRepliesStillParse()
    {
        var reply = AiResponseParser.ParseReplyText("{\"reply\":\"ได้\",\"relationshipDelta\":0,\"referencedFactIds\":[],\"worldIntent\":{\"speechAct\":\"request\",\"verb\":\"move\",\"targetId\":\"desk\",\"relation\":\"between\",\"referenceId\":\"door\"}}");
        Assert.That(reply.worldIntent.IsOffered(Menu), Is.True);
        Assert.That(AiResponseParser.ParseReplyText("{\"reply\":\"ได้\",\"relationshipDelta\":0}").worldIntent, Is.Null);
        Assert.That(AiResponseParser.ParseReplyText("{\"reply\":\"say \\\"worldIntent\\\" : hello\",\"relationshipDelta\":0}").worldIntent, Is.Null);
        var invalid = AiResponseParser.ParseReplyText("{\"reply\":\"ได้\",\"relationshipDelta\":0,\"companionAction\":\"world:inspect:at:desk\",\"worldIntent\":{}}");
        Assert.That(invalid.worldIntent, Is.Not.Null);
        CompanionSemanticIntent.SanitizeProposal(invalid, Menu); Assert.That(invalid.companionAction, Is.Null);
    }
    [TestCase("request", "inspect", "desk", "at", "", true)]
    [TestCase("request", "open", "desk", "at", "", false)]
    [TestCase("request", "move", "desk", "between", "door", true)]
    [TestCase("request", "move", "desk", "between", "desk", false)]
    [TestCase("request", "move", "desk", "between", "hidden", false)]
    [TestCase("request", "inspect", "desk", "between", "door", false)]
    [TestCase("request", "move", "hidden", "at", "", false)]
    [TestCase("request", "move", "desk", "at", "door", false)]
    [TestCase("request", "move", "desk", "teleport", "", false)]
    [TestCase("question", "none", "", "at", "", true)]
    [TestCase("none", "none", "", "at", "", true)]
    [TestCase("question", "open", "desk", "at", "", false)]
    public void OnlyOfferedCapabilitiesAndWellFormedIntentsSurvive(string speech, string verb, string target, string relation, string reference, bool expected)
    {
        Assert.That(new CompanionSemanticIntent { speechAct = speech, verb = verb, targetId = target, relation = relation, referenceId = reference }.IsOffered(Menu), Is.EqualTo(expected));
    }
    [Test]
    public void InactiveIntentPreservesLegacyActionsButQuestionsAndInvalidIntentsCannotFallThrough()
    {
        var reply = new GeneratedChatReply { companionAction = "world:inspect:at:desk", worldIntent = new CompanionSemanticIntent {
            speechAct = "none", verb = "none", targetId = "", relation = "at", referenceId = "" } };
        CompanionSemanticIntent.SanitizeProposal(reply, Menu);
        Assert.That(reply.worldIntent, Is.Null); Assert.That(reply.companionAction, Is.EqualTo("world:inspect:at:desk"));
        reply.worldIntent = new CompanionSemanticIntent { speechAct = "question", verb = "none", targetId = "", relation = "at", referenceId = "" };
        CompanionSemanticIntent.SanitizeProposal(reply, Menu); Assert.That(reply.companionAction, Is.Null);
        reply.companionAction = "world:inspect:at:desk";
        reply.worldIntent = new CompanionSemanticIntent { speechAct = "request", verb = "open", targetId = "hidden", relation = "at", referenceId = "" };
        CompanionSemanticIntent.SanitizeProposal(reply, Menu); Assert.That(reply.companionAction, Is.Null); Assert.That(reply.worldIntent, Is.Null);
    }
}
