using System.Collections.Generic;
using System.Text;
using MysteryGame.Core;

namespace MysteryGame.Knowledge
{
    /// <summary>
    /// Exactly what one NPC is allowed to say right now: the facts they may
    /// reference, the facts they must not, the single next step of the puzzle,
    /// and the deterministic hint the model is only permitted to rephrase.
    /// </summary>
    public class NpcKnowledgeContext
    {
        public RoomKnowledgeData Room;
        public NpcProfileData Npc;
        public string NpcId = string.Empty;

        public readonly List<RoomFact> KnownFacts = new List<RoomFact>();
        public readonly List<RoomFact> WithheldFacts = new List<RoomFact>();

        public PuzzleStep CurrentStep;
        public int CompletedSteps;
        public int TotalSteps;

        public HintLevel AllowedHintLevel = HintLevel.None;
        public string DeterministicHint = string.Empty;
        public string Tone = string.Empty;
        public bool PlayerAskedForHint;

        public bool HasData
        {
            get { return Room != null; }
        }

        public bool CanReference(string factId)
        {
            if (string.IsNullOrWhiteSpace(factId))
            {
                return false;
            }

            foreach (RoomFact fact in KnownFacts)
            {
                if (fact != null && fact.factId == factId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Rejects a model reply that cites knowledge this NPC does not have.
        /// </summary>
        public bool ValidateReferences(IEnumerable<string> referencedFactIds,
                                       out string offendingFactId)
        {
            offendingFactId = null;
            if (referencedFactIds == null)
            {
                return true;
            }

            foreach (string id in referencedFactIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (!CanReference(id))
                {
                    offendingFactId = id;
                    return false;
                }
            }

            return true;
        }

        public string ToPromptSection()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("=== ข้อมูลความจริงของห้อง (CANON — ห้ามแต่งเพิ่ม) ===");
            sb.AppendLine("ห้อง: " + Room.roomName + " (" + Room.roomId + ")");
            if (!string.IsNullOrWhiteSpace(Room.roomDescription))
            {
                sb.AppendLine(Room.roomDescription);
            }
            sb.AppendLine();

            sb.AppendLine("--- สิ่งที่ตัวละครนี้ 'รู้' และพูดถึงได้ (อ้างด้วย factId) ---");
            if (KnownFacts.Count == 0)
            {
                sb.AppendLine("(ยังไม่รู้อะไรเกี่ยวกับห้องนี้เลย)");
            }
            else
            {
                foreach (RoomFact fact in KnownFacts)
                {
                    sb.AppendLine("[" + fact.factId + "] " + fact.statement);
                }
            }
            sb.AppendLine();

            if (WithheldFacts.Count > 0)
            {
                sb.AppendLine("--- สิ่งที่ตัวละครนี้ 'ยังไม่รู้' หรือ 'ห้ามเปิดเผย' ---");
                foreach (RoomFact fact in WithheldFacts)
                {
                    sb.AppendLine("[" + fact.factId + "] ห้ามพูดถึงหรือบอกใบ้ถึงเรื่องนี้โดยเด็ดขาด");
                }
                sb.AppendLine("ถ้าผู้เล่นถามถึงเรื่องเหล่านี้ ให้ตอบตามจริงว่าไม่รู้ ห้ามเดา ห้ามแต่ง");
                sb.AppendLine();
            }

            sb.AppendLine("--- ความคืบหน้าของปริศนา ---");
            sb.AppendLine("ทำสำเร็จไปแล้ว " + CompletedSteps + "/" + TotalSteps + " ขั้น");
            if (CurrentStep != null)
            {
                sb.AppendLine("ขั้นตอนถัดไปที่ผู้เล่นทำได้จริงตอนนี้: " + CurrentStep.summary);
            }
            else
            {
                sb.AppendLine("ผู้เล่นทำครบทุกขั้นของห้องนี้แล้ว");
            }
            sb.AppendLine();

            sb.AppendLine("--- สิทธิ์ในการให้คำใบ้ ---");
            if (!PlayerAskedForHint)
            {
                sb.AppendLine("ผู้เล่นไม่ได้ขอคำใบ้ในข้อความนี้ **ห้ามใส่คำใบ้ ห้ามชวนไปสำรวจสิ่งใด** ให้คุยตามบุคลิกเท่านั้น");
            }
            else if (AllowedHintLevel == HintLevel.None ||
                     string.IsNullOrWhiteSpace(DeterministicHint))
            {
                sb.AppendLine("ตัวละครนี้ **ไม่ให้คำใบ้**");
                if (Npc != null && !string.IsNullOrWhiteSpace(Npc.refuseHintLine))
                {
                    sb.AppendLine("ให้ตอบปฏิเสธในแนวนี้ (เรียบเรียงใหม่ได้): " +
                                  Npc.refuseHintLine);
                }
            }
            else
            {
                sb.AppendLine("ระดับคำใบ้ที่อนุญาตตอนนี้: " + AllowedHintLevel);
                sb.AppendLine("**คำใบ้ที่อนุญาตมีเพียงข้อความนี้เท่านั้น** คุณมีหน้าที่เรียบเรียงใหม่ให้เข้ากับบุคลิก ห้ามเพิ่มข้อมูลอื่น ห้ามข้ามขั้น:");
                sb.AppendLine("\"" + DeterministicHint + "\"");
            }
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(Tone))
            {
                sb.AppendLine("--- อารมณ์ตามระดับความสัมพันธ์ ---");
                sb.AppendLine(Tone);
                sb.AppendLine();
            }

            sb.AppendLine("--- กติกาการตอบ ---");
            sb.AppendLine("1. ห้ามคิดค้นไอเท็ม สถานที่ หรือข้อเท็จจริงใหม่ที่ไม่มีในรายการข้างบน");
            sb.AppendLine("2. ถ้าไม่มีข้อมูล ให้บอกตามตรงว่าไม่รู้");
            sb.AppendLine("3. ส่ง referencedFactIds เป็นรายการ factId ที่คุณอ้างถึงจริงในคำตอบ (ถ้าไม่ได้อ้างถึงเลย ให้ส่งเป็นรายการว่าง)");

            return sb.ToString();
        }
    }
}
