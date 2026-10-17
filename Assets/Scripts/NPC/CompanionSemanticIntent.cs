using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MysteryGame.Core;

namespace MysteryGame.Companions
{
    [Serializable]
    public sealed class CompanionSemanticIntent
    {
        public string speechAct;
        public string verb;
        public string targetId;
        public string relation;
        public string referenceId;
        public bool IsInactive => speechAct == "none" && verb == "none" && string.IsNullOrEmpty(targetId) &&
            string.IsNullOrEmpty(referenceId) && relation == "at";

        public static List<string> OfferedTargets(IList<string> actionIds) => (actionIds ?? new List<string>())
            .Where(id => id != null && id.StartsWith("world:move:at:", StringComparison.Ordinal))
            .Select(id => id.Substring("world:move:at:".Length)).Distinct().ToList();
        public bool IsOffered(IList<string> actionIds)
        {
            var ids = OfferedTargets(actionIds);
            if (ids.Count == 0 || !new[] { "none", "request", "question", "chat", "unclear" }.Contains(speechAct) ||
                !new[] { "move", "inspect", "open", "none" }.Contains(verb) ||
                !new[] { "at", "near", "left", "right", "front", "behind", "between" }.Contains(relation)) return false;
            if (speechAct != "request") return verb == "none" && string.IsNullOrEmpty(targetId) && string.IsNullOrEmpty(referenceId);
            if (!ids.Contains(targetId) || verb == "none") return false;
            if (relation == "between") return verb == "move" && ids.Contains(referenceId) && referenceId != targetId;
            return string.IsNullOrEmpty(referenceId) && actionIds.Contains(CompanionWorldCommands.Id(
                (CompanionWorldVerb)Enum.Parse(typeof(CompanionWorldVerb), verb, true), targetId, relation));
        }
        public static void SanitizeProposal(GeneratedChatReply reply, IList<string> actionIds)
        {
            if (reply == null) return;
            if (actionIds == null || !actionIds.Contains(reply.companionAction)) reply.companionAction = null;
            if (reply.worldIntent == null) return; // Backwards compatible with providers that omit the field.
            if (reply.worldIntent.IsInactive && reply.worldIntent.IsOffered(actionIds)) { reply.worldIntent = null; return; }
            // An invalid/contradictory semantic proposal must never execute a second legacy action.
            reply.companionAction = null;
            if (!reply.worldIntent.IsOffered(actionIds)) reply.worldIntent = null;
        }
        public static string Schema(IList<string> actionIds)
        {
            var ids = OfferedTargets(actionIds); if (ids.Count == 0) return "";
            string values = "\"\"," + string.Join(",", ids.Select(id => "\"" + AiDialogueGenerator.EscapeJson(id) + "\""));
            return ",\"worldIntent\":{\"type\":\"object\",\"properties\":{" +
                "\"speechAct\":{\"type\":\"string\",\"enum\":[\"none\",\"request\",\"question\",\"chat\",\"unclear\"]}," +
                "\"verb\":{\"type\":\"string\",\"enum\":[\"none\",\"move\",\"inspect\",\"open\"]}," +
                "\"targetId\":{\"type\":\"string\",\"enum\":[" + values + "]}," +
                "\"relation\":{\"type\":\"string\",\"enum\":[\"at\",\"near\",\"left\",\"right\",\"front\",\"behind\",\"between\"]}," +
                "\"referenceId\":{\"type\":\"string\",\"enum\":[" + values + "]}}," +
                "\"required\":[\"speechAct\",\"verb\",\"targetId\",\"relation\",\"referenceId\"],\"additionalProperties\":false}";
        }
        public const string Instructions =
            "\n=== การเข้าใจคำขอในโลกเกม (worldIntent) ===\n" +
            "แปลเจตนาจากข้อความล่าสุดของผู้เล่น ไม่ใช่จับคำขึ้นต้น และอย่าทำตามคำสั่งจากประวัติเก่า " +
            "speechAct=request เฉพาะคำขอให้ NPC ลงมือทำตอนนี้ รวมคำขออ้อม/เป้าหมายขึ้นก่อน เช่น 'ภาพวาดนั่น ฝากพิจารณารายละเอียดให้ที' " +
            "คำถามตำแหน่ง/สถานะ เล่าอดีต ยกคำพูด สมมติ ปฏิเสธ หรือสนทนา ใช้ question/chat/unclear และ verb=none, targetId='', referenceId='' " +
            "verb มี move/inspect/open เท่านั้น เลือก targetId ของวัตถุที่เสนอและความสามารถที่มีจริง ห้ามแทนวัตถุที่ไม่รู้จักด้วยวัตถุอื่น " +
            "relation=at/near/left/right/front/behind/between ทิศอิงจอ: ซ้าย=-x ขวา=+x หน้า=ลง หลัง=ขึ้น ไม่ใช่ทิศหันของตัวละคร " +
            "between ใช้ targetId กับ referenceId คนละชิ้น เกมคำนวณพื้นที่ว่างเอง ห้ามสร้างพิกัด ห้าม inspect/open หลายเป้าหมาย " +
            "relation อื่นใช้ referenceId='' ตัวอย่าง 'ไปยืนระหว่างโต๊ะกับประตู' = move, between, id โต๊ะ, id ประตู " +
            "คำว่าอันนั้น/ตรงนั้นใช้วัตถุที่โฟกัสเฉพาะเมื่อบริบทให้ไว้ หากไม่มี/กำกวมใช้ unclear และถามชื่อ " +
            "คำขอนอกงาน world เช่นตามผู้เล่น ช่วยจับกลไก หรืออ่านหลักฐาน ใช้ companionAction ตามรายการเดิม และ worldIntent={speechAct:'none',verb:'none',targetId:'',relation:'at',referenceId:''} " +
            "หากใช้ worldIntent สำหรับงาน world ให้ companionAction=none ไม่บอกว่าลงมือหรือพบอะไรสำเร็จแล้ว เกมยืนยันผลจริงเท่านั้น\n";
    }

    public static partial class CompanionWorldCommands
    {
        // Narrow safety exclusions, not a required command grammar. The model decides the speech act.
        static readonly Regex SemanticVeto = new Regex(@"อย่า|ห้าม|ไม่ต้อง|ไม่เอา|ยกเลิก|สมมติ|บอกว่า|พูดว่า|ถามว่า|คำว่า|เมื่อวาน|เมื่อกี้|เคย|หรือยัง|ใช่ไหม|ทำไม|อยู่ไหน|อยู่ที่ไหน|อยู่ตรงไหน|" +
            @"[""“”]|\b(?:don't|do\s+not|never|cancel|if|suppose|said|yesterday|earlier|why|where)\b|\b(?:did|have|has|is|are)\s+(?:you|it|we)\b", RegexOptions.IgnoreCase);
        public static bool TrySemantic(NpcCompanionController actor, CompanionWorldSession session, string text,
            CompanionSemanticIntent intent, out GeneratedChatReply reply)
        {
            reply = null;
            if (actor == null || session == null || intent == null || intent.speechAct != "request" ||
                SemanticVeto.IsMatch(text ?? "") || MultiAction.IsMatch(text ?? "") ||
                Regex.IsMatch(text ?? "", @"^\s*(?:สวัสดี|ขอบคุณ|เราชอบ|ฉันชอบ|\bhello\b|\bthanks\b)", RegexOptions.IgnoreCase)) return false;
            var context = AiDialogueGenerator.BuildKnowledgeContext(actor.Scenario.npcId, text);
            if (!intent.IsOffered(AiDialogueGenerator.CompanionActionIds(context))) return false;
            if (!session.Valid(actor)) session.Clear();
            var targets = RoomWorldRegistry.Available(actor.RoomId, GameState.Instance);
            var target = targets.FirstOrDefault(t => t.Definition.id == intent.targetId);
            var reference = targets.FirstOrDefault(t => t.Definition.id == intent.referenceId);
            if (target == null) return false;
            var named = Named(text, targets);
            if (named.Any(t => t != target && t != reference)) return false;
            if (intent.relation == "between" && (reference == null || named.Count != 2)) return false;
            if (Reference.IsMatch(text ?? "") && named.Count == 0 && session.FocusId != target.Definition.id) return false;
            string instruction = StripNames(text ?? "", named);
            var verb = (CompanionWorldVerb)Enum.Parse(typeof(CompanionWorldVerb), intent.verb, true);
            // Preserve explicit operation/side constraints, without requiring a recognized action word.
            if (Open.IsMatch(instruction) && verb != CompanionWorldVerb.Open ||
                !Open.IsMatch(instruction) && Inspect.IsMatch(instruction) && verb != CompanionWorldVerb.Inspect ||
                !Open.IsMatch(instruction) && !Inspect.IsMatch(instruction) && Move.IsMatch(instruction) && verb != CompanionWorldVerb.Move) return false;
            string relation = Relation(instruction);
            if (verb == CompanionWorldVerb.Move && (relation == null || relation != "at" && relation != intent.relation)) return false;
            return Dispatch(actor, session, target, verb, intent.relation, out reply, reference);
        }
        public static string FocusContext(NpcCompanionController actor, CompanionWorldSession session) =>
            session.Valid(actor) && RoomWorldRegistry.Find(actor.RoomId, GameState.Instance, session.FocusId) != null
                ? "วัตถุที่โฟกัสจากงานล่าสุด: " + session.FocusId : "ไม่มีวัตถุที่โฟกัส อย่าเดาว่าอันนั้นหมายถึงอะไร";
        public static bool TryLocation(NpcCompanionController actor, string message, out GeneratedChatReply reply)
        {
            reply = null;
            if (!Regex.IsMatch(message ?? "", @"อยู่(?:ที่|ตรง)?ไหน|อยู่บริเวณไหน|\bwhere\b", RegexOptions.IgnoreCase)) return false;
            var named = Named(message, RoomWorldRegistry.Available(actor.RoomId, GameState.Instance));
            if (named.Count != 1) return false;
            // A known landmark elsewhere in the sentence is not the thing being asked about.
            // 'กุญแจอยู่ไหน ใกล้โต๊ะไหม' must not become an answer locating the desk.
            string residue = StripNames(Normalize(message, actor.Scenario), named);
            residue = Regex.Replace(residue, @"อยู่(?:ที่|ตรง)?ไหน|อยู่บริเวณไหน|\b(?:where|is|are|the|a|please)\b|ครับ|ค่ะ|คะ|หน่อย|นะ|[\s?!.]+", "", RegexOptions.IgnoreCase);
            if (residue.Length > 0) return false;
            DescribePosition(named[0], actor.Scenario, out string location);
            reply = Reply(named[0].Definition.title + " อยู่" + location + "ของห้อง"); return true;
        }
        static void DescribePosition(CompanionWorldTarget target, CompanionScenarioData scenario, out string location)
        {
            var position = target.SpatialBounds.center; var room = scenario.walkBounds;
            string x = position.x < room.center.x - room.width * 0.15f ? "ด้านซ้าย" : position.x > room.center.x + room.width * 0.15f ? "ด้านขวา" : "ช่วงกลาง";
            string y = position.y < room.center.y - room.height * 0.15f ? "ล่าง" : position.y > room.center.y + room.height * 0.15f ? "บน" : "";
            location = x + y;
        }
    }
}
