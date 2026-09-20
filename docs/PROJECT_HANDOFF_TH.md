# AI Mystery Game — สถานะงานและแผนส่งต่อ

เอกสารนี้ใช้เป็นจุดเริ่มต้นสำหรับสมาชิกที่ clone/pull โปรเจกต์ไปทำต่อ

## เป้าหมายของต้นแบบ

เกม escape room แบบ 2D top-down ที่ผู้เล่นสำรวจห้อง แก้ลำดับปริศนา และคุยกับ NPC ได้ทั้งผ่านตัวเลือกและการพิมพ์ข้อความ NPC มีความสัมพันธ์ ความทรงจำ ความต้องการ อารมณ์ และสามารถส่งสัญญาณชวนคุยเพื่อเริ่ม mini event ได้

หลักสำคัญของระบบ AI:

- ข้อเท็จจริงของด่าน คำใบ้ และวิธีผ่านต้องมาจากข้อมูลที่ผู้พัฒนาเขียนไว้
- AI ใช้สำหรับเรียบเรียงบทพูด บุคลิก อารมณ์ และความหลากหลายของประโยค
- AI ห้ามเป็นผู้คิดกฎปริศนา ไอเท็ม หรือวิธีผ่านขึ้นมาเอง
- หาก AI ใช้งานไม่ได้ เกมต้องเล่นต่อได้ด้วยบทสนทนาสำรอง

## สิ่งที่ทำแล้ว

### การเล่นและฉาก

- มีสามฉาก: `Assets/Scenes/Room01.unity` (ห้องทำงาน), `Room02.unity` (หอสมุดแห่งดวงดาว) และ `Room03.unity` (ยังเป็นโครงเปล่า)
- เริ่มเล่นที่ `Room01.unity` และเปลี่ยนฉากด้วย `RoomTransitionManager`
- `IntroSequence` สร้างตัวเองอัตโนมัติเมื่อ Room01 โหลด แสดงบทนำบนจอดำแล้วค่อยเฟดเข้าห้อง (กด Space/Esc ข้ามได้) แก้ข้อความได้ที่ `Assets/Scripts/UI/IntroSequence.cs`
- เดินด้วย WASD/ปุ่มลูกศร และกด E เพื่อสำรวจหรือคุย
- ฉากเป็นห้อง 2D top-down
- ตัวละครในห้องเป็น chibi ที่มีการขยับเดินแบบ procedural
- ภาพตัวละครเต็มแสดงเฉพาะระหว่างบทสนทนาและเด้งตามช่วงที่พูด

### ลำดับผ่านด่าน Room01

1. ตรวจภาพวาด → ได้ flag `inspected_painting`
2. ตรวจโต๊ะ → ต้องมี `inspected_painting` แล้วจึงได้ flag `found_note`
3. เปิดลิ้นชัก → ต้องมี `found_note` แล้วจึงได้ item `key`
4. เปิดประตู → ต้องมี `key` แล้วจึงตั้ง flag `door_unlocked` และเปลี่ยนเป้าหมายเป็น `escape_room_complete`

ข้อมูลลำดับนี้อยู่ใน `Assets/Data/Interactions/`

### ลำดับผ่านด่าน Room02 (หอสมุดร้าง)

ห้องนี้ใช้ปริศนาแบบหลายขั้น โดยแต่ละขั้นจะ "ทำไม่ได้" จนกว่าขั้นก่อนหน้าจะสำเร็จ

1. คุยกับเซนะ (นางฟ้าเฝ้าประตู) → ตั้ง flag `sena_wants_tome` นางเรียก "จารึกที่ยังเขียนมิจบ" เป็นของถวาย และจะยังไม่เอ่ยปริศนา
2. อ่านสมุดทะเบียนบนโต๊ะอ่านหนังสือ → ต้องมี `sena_wants_tome` แล้วจึงได้ flag `read_ledger` (รู้ว่าหนังสืออยู่ชั้นล่างสุดของตู้ฝั่งซ้าย)
3. หยิบหนังสือจากตู้หนังสือชั้นล่างสุด → ต้องมี `read_ledger` แล้วจึงได้ item `tome` (interaction นี้ `repeatable: 0`)
4. คุยกับเซนะขณะถือ `tome` → `SenaInteraction` หักไอเท็มออก ตั้ง flag `sena_offering_given` แล้วเปลี่ยนไปใช้ `Sena_Riddle.asset` ซึ่งเอ่ยปริศนา
5. พิมพ์คำตอบ `Tomorrow` / `พรุ่งนี้` ในช่องแชท → ตั้ง flag `sena_passed` เซนะค่อยๆ จางหาย
6. สำรวจประตูดวงดาว → ต้องมี `sena_passed` แล้วจึงเปลี่ยนฉากไป Room03

คำตอบปริศนาจะถูกรับก็ต่อเมื่อ `sena_offering_given` เป็นจริงแล้วเท่านั้น (`SenaInteraction.IsListeningForAnswer`) ดังนั้นการเดาคำตอบก่อนมอบของถวายจะไม่ผ่านด่าน

เบาะแสของปริศนา (ไม่บังคับ แต่ควบคุมว่า Alice จะใบ้ลึกแค่ไหน) อยู่ที่นาฬิกาโบราณ บทกวีบนตู้หนังสือชั้นกลาง และศิลาจารึกที่แท่นไฟ — นับจำนวนด้วย `DialogueManager.CountRoom02CluesFound()`

เซนะใช้ DialogueData สองไฟล์ตามสถานะ (`Sena_Demand.asset` / `Sena_Riddle.asset`) และพูดด้วยสำนวนโบราณตามบุคลิกที่กำหนดไว้ใน `AiDialogueGenerator.BuildReplyPrompt`

### ฉากและการชนกับเฟอร์นิเจอร์

- ภาพห้องที่เห็นจริงมาจาก background ภาพเดียวที่ `RoomVisualController` สร้างขึ้น ส่วน SpriteRenderer ของ prop ทุกชิ้นที่อยู่ใต้ root `Environment` / `Interactables` จะถูกปิดตอนรันไทม์
- **background ของแต่ละห้องอยู่ในไฟล์ `RoomKnowledgeData` ของห้องนั้น** (`Assets/Resources/Knowledge/Rooms/<roomId>.asset` ช่อง `background`) แก้ที่เดียวพอ
  - `RoomVisualController` เป็น `DontDestroyOnLoad` ตัวที่รอดคือตัวจาก scene แรกที่โหลด ดังนั้นช่อง `room01Background` / `room02Background` / `room03Background` ที่อยู่บน component ในแต่ละ scene เป็นแค่ fallback สำหรับห้องที่ยังไม่มีไฟล์ knowledge เท่านั้น และต้องตั้งให้เหมือนกันทุก scene ไม่งั้นห้องจะแสดงภาพผิดตามลำดับการโหลด
- ตำแหน่งของ GameObject ในฉากจึงมีหน้าที่เดียวคือวาง collider ให้ตรงกับเฟอร์นิเจอร์ที่วาดไว้ในภาพพื้นหลัง
- เฟอร์นิเจอร์ที่เดินทะลุไม่ได้ใช้ GameObject ชื่อ `Obstacle_*` ใต้ `Environment` ซึ่งมี BoxCollider2D แบบไม่ใช่ trigger และไม่มี SpriteRenderer
- กำแพง (`Wall_Top` / `Wall_Bottom` / `Wall_Left` / `Wall_Right`) ถูกตัดให้ตรงกับขอบพื้นในภาพ ดังนั้นเมื่อย้ายเฟอร์นิเจอร์หรือเปลี่ยนภาพพื้นหลัง ต้องปรับ collider ตามด้วย
- อาการ "เดินชนกำแพงล่องหน" เกิดได้สองแบบ: (1) ภาพพื้นหลังไม่ตรงกับที่ collider ถูกคำนวณไว้ และ (2) มีพื้นที่พื้นที่เดินไปไม่ถึงเพราะถูกล้อมด้วย collider จนหมด — ตรวจได้ด้วยการ flood-fill จากจุดเกิดของผู้เล่นแล้วหา free cell ที่ไม่ถูก visit
- การแปลงพิกัด: ภาพ 16:9 ที่ PPU 100 กับกล้อง orthographic size 5 จะครอบคลุม x ∈ [-8.89, 8.89] และ y ∈ [-5, 5] ดังนั้น `world_x = (px - width/2) * 17.778/width` และ `world_y = (height/2 - py) * 10/height`

### บทสนทนาและสถานะ NPC

- มี NPC สองตัว: Alice (เพื่อนร่วมทาง ตามผู้เล่นไปทุกห้อง) และเซนะ (ผู้เฝ้าประตูใน Room02)
- มีค่าความสัมพันธ์ระหว่างผู้เล่นกับ NPC
- มีความทรงจำของ NPC และประวัติการกระทำของผู้เล่น
- บทเปิดเปลี่ยนเมื่อกลับมาคุยตามความสัมพันธ์และการเปลี่ยนแปลงของห้อง
- ผู้เล่นพิมพ์คุยเองได้ และ AI ส่งกลับ `reply` พร้อม `relationshipDelta`
- มี local fallback เมื่อ AI ใช้งานไม่ได้

### Mini event

- NPC ส่งสัญญาณว่าอยากคุย โดยไม่ขัดจังหวะการเดินหรือแก้ปริศนา
- มีระบบ trigger, weight, cooldown และเวลาหมดอายุของคำชวน
- Alice มีตัวอย่าง event หิวน้ำและคิดถึงบ้าน
- ระบบรองรับชนิด trigger สำหรับ need, emotion, NPC conflict, เวลาในห้อง และเหตุการณ์สุ่ม
- เซนะยังไม่มี mini event ของตัวเอง และยังไม่มีข้อมูล event ทะเลาะกันระหว่าง NPC

### AI provider

- รองรับ OpenAI Responses API
- รองรับ KKU IntelSphere แบบ OpenAI-compatible
- KKU endpoint ที่ถูกต้องคือ `https://gen.ai.kku.ac.th/api/v1/chat/completions`
- รายการโมเดลของ KKU อยู่ที่ `https://gen.ai.kku.ac.th/api/v1/models`
- รองรับ custom OpenAI-compatible endpoint
- API key เก็บในหน่วยความจำเฉพาะรอบการเล่น ไม่บันทึกลง Scene, PlayerPrefs หรือ Git
- หน้าตั้งค่ามีปุ่มทดสอบการเชื่อมต่อและแสดงข้อความ error จาก provider

### NPC World Knowledge (P0 — ทำแล้ว)

canon ของห้องย้ายออกจากโค้ดมาเป็น ScriptableObject แล้ว ไม่ต้องแก้ `BuildReplyPrompt` เวลาเพิ่มห้องใหม่อีก

- `RoomKnowledgeData` (`Assets/Scripts/Knowledge/RoomKnowledgeData.cs`)
  - `facts[]`: `factId`, ข้อความ canon, `revealedWhen` (ใช้ `ConditionRule` ชุดเดียวกับระบบ interaction), `isPuzzleAnswer`
  - `steps[]`: ลำดับผ่านด่าน แต่ละขั้นมี `availableWhen` / `completedWhen` และคำใบ้สามระดับ (`vagueHint` / `normalHint` / `explicitHint`)
- `NpcProfileData` (`Assets/Scripts/Knowledge/NpcProfileData.cs`)
  - บุคลิก สำนวนการพูด เป้าหมายส่วนตัว
  - `knownFactIds` (รู้ตั้งแต่ต้น), `learnedFacts` (รู้เมื่อเงื่อนไขเป็นจริง), `forbiddenFactIds` (รู้แต่ห้ามพูด)
  - `givesHints` + `refuseHintLine` สำหรับตัวละครอย่างเซนะที่ไม่ใบ้เด็ดขาด
  - `tones[]` แบ่งตามช่วงความสัมพันธ์ พร้อม `maxHintLevel` ของแต่ละช่วง
- `NpcKnowledgeContextBuilder` อ่าน `GameState` แล้วคัดออกมาเป็น `NpcKnowledgeContext` ซึ่งมีเฉพาะ fact ที่ตัวละครนั้น "รู้ + ถูกเปิดเผยแล้ว + ไม่ถูกห้าม", ขั้นตอนถัดไปที่ทำได้จริง, และ **คำใบ้ที่เลือกมาแบบ deterministic**
- `AiDialogueGenerator.BuildReplyPrompt` ใส่ section นี้ลง prompt และสั่งให้ AI **เรียบเรียงคำใบ้ที่ให้มาเท่านั้น** ห้ามคิดเอง
- AI ต้องส่ง `referencedFactIds` กลับมาด้วย ถ้าอ้าง factId ที่ตัวละครไม่มีสิทธิ์รู้ ระบบจะทิ้งคำตอบนั้นแล้วใช้ fallback แทน (`NpcKnowledgeContext.ValidateReferences`)
- `DialogueManager.BuildFallbackReply` อ่านคำใบ้จากแหล่งเดียวกัน ดังนั้นตอนไม่มี API key ผู้เล่นก็ได้คำใบ้ขั้นเดียวกันเป๊ะ

ไฟล์ข้อมูลอยู่ที่ `Assets/Resources/Knowledge/Rooms/<roomId>.asset` และ `Assets/Resources/Knowledge/Npcs/<npcId>.asset` โหลดผ่าน `KnowledgeLibrary` (ชื่อไฟล์ต้องตรงกับชื่อ scene และ `DialogueData.speakerId`)

ห้องที่ยังไม่มีไฟล์ knowledge จะตกไปใช้ prompt แบบเดิมโดยอัตโนมัติ (ตอนนี้คือ Room03)

### การทดสอบ (P0 — ทำแล้ว)

- `Assets/Tests/Editor/NpcKnowledgeTests.cs` (EditMode, 14 เคส) เปิดผ่าน `Window > General > Test Runner`
- ครอบคลุมตามที่เอกสารกำหนด: ก่อน/หลังตรวจภาพวาด, ก่อน/หลังพบโน้ต, รหัสลิ้นชักไม่รั่วก่อนเวลา, ลำดับขั้นของทั้งสองห้องเดินถูกทาง, เซนะไม่ใบ้แม้ความสัมพันธ์เต็ม, เซนะห้ามบอกเลขชั้นหนังสือ, คำตอบที่อ้าง fact นอก canon ถูกปฏิเสธ, และห้องที่ยังไม่มีข้อมูลต้องไม่พัง
- โปรเจกต์ถูกแบ่งเป็นสอง assembly: `MysteryGame.Runtime` (`Assets/Scripts/`) และ `MysteryGame.Tests.EditMode` (`Assets/Tests/Editor/`) เพราะ test assembly อ้างถึง Assembly-CSharp โดยตรงไม่ได้

### AI provider (P1 — ทำแล้ว)

- ปุ่ม "โหลดรายชื่อโมเดลที่บัญชีนี้ใช้ได้" เรียก `GET /models` ของ provider แล้วแสดงเป็นรายการให้เลือก (KKU, Gemini, custom OpenAI-compatible)
- `AiProviderDiagnostics` แยกสาเหตุความผิดพลาดเป็น key ผิด / model ผิด / เรียกถี่เกินไป / โควตาหมด / เน็ตมีปัญหา / เซิร์ฟเวอร์ล่ม แล้วแสดงข้อความที่บอกว่าควรทำอะไรต่อ
- ข้อความ error ทุกอันถูกกรองด้วย `AiProviderDiagnostics.Redact` ก่อนแสดงหรือ log เพื่อไม่ให้ API key หลุด

## ปัญหาหลักในสถานะปัจจุบัน

canon ของ Room01 และ Room02 ย้ายไปเป็น ScriptableObject แล้ว และคำตอบของ AI ถูกตรวจก่อนถึงผู้เล่น สิ่งที่ยังขาดอยู่คือ:

- **ประวัติสนทนาหลายข้อความ** — ตอนนี้ส่งความทรงจำล่าสุดให้ AI แค่หนึ่งรายการ ทำให้ NPC จำบทสนทนาก่อนหน้าไม่ได้จริง
- **ประวัติและความลับส่วนตัวของ NPC** — `NpcProfileData` มีช่องบุคลิกแล้ว แต่ยังไม่มีโครงสำหรับ backstory หรือความลับที่ค่อยๆ เปิดเผย
- **Room03 ยังไม่มีไฟล์ knowledge** — ยังตกไปใช้ prompt แบบ hard-code เดิม ต้องสร้าง `Assets/Resources/Knowledge/Rooms/Room03.asset` เมื่อออกแบบห้องเสร็จ
- **fallback ที่เขียนมือใน `DialogueManager`** — ส่วนทักทาย/ตอบโต้คำหยาบยังเป็นโค้ด ควรย้ายไปเป็นข้อมูลใน `NpcProfileData` ด้วย

## งานถัดไปตามลำดับความสำคัญ

### P0 — งานที่เหลือของ NPC World Knowledge

โครงหลักทำเสร็จแล้ว (ดูหัวข้อ "NPC World Knowledge" ด้านบน) ที่เหลือคือ:

1. ส่งประวัติสนทนาหลายข้อความให้ AI
   - ตอนนี้ `BuildReplyPrompt` ส่งความทรงจำล่าสุดแค่หนึ่งรายการ
   - ควรเก็บ log บทสนทนาต่อ NPC แล้วส่งท้าย N ข้อความ พร้อมจำกัดความยาว context

2. เพิ่มประวัติและความลับใน `NpcProfileData`
   - backstory ที่ NPC เล่าได้
   - ความลับที่ค่อยๆ เปิดเผยตามความสัมพันธ์หรือ flag (ใช้ `KnowledgeGrant` แบบเดียวกับ `learnedFacts` ได้)

3. ย้าย fallback ที่ยังเขียนมือใน `DialogueManager.BuildFallbackReply` ไปเป็นข้อมูล
   - ส่วนทักทาย ตอบโต้คำหยาบ และการขอโทษ ยังเป็นโค้ดอยู่
   - ควรเป็นรายการใน `NpcProfileData` เพื่อให้เพิ่ม NPC ใหม่ได้โดยไม่แตะโค้ด

4. สร้าง `Assets/Resources/Knowledge/Rooms/Room03.asset` เมื่อออกแบบ Room03 เสร็จ
   - ถ้ายังไม่มีไฟล์ ห้องนั้นจะตกไปใช้ prompt แบบ hard-code เดิมโดยอัตโนมัติ

### P0 — งานที่เหลือของการทดสอบ

ชุดทดสอบ knowledge มีแล้วที่ `Assets/Tests/Editor/NpcKnowledgeTests.cs` ที่ยังขาด:

- เคสที่ API ล้มเหลวหรือคืน JSON ผิดรูป แล้วต้องไม่ทำให้บทสนทนาค้าง (ต้อง mock `UnityWebRequest` หรือแยก parser ออกมาทดสอบเดี่ยว)
- เคสที่ตรวจว่าเงื่อนไขของ `InteractionData` ใน `Assets/Data/Interactions/` ตรงกับลำดับขั้นใน `RoomKnowledgeData` (กันข้อมูลสองชุดเพี้ยนจากกัน)

### P1 — งานที่เหลือของระบบ KKU

ปุ่มโหลดรายชื่อโมเดลและการแยกข้อความ error ทำแล้ว ที่ยังขาด:

- แสดง quota ที่เหลือ หาก response ของ provider มีข้อมูลดังกล่าว
- จำโมเดลที่เลือกไว้ข้ามรอบการเล่น (ปัจจุบันจำเฉพาะ provider/endpoint ผ่าน PlayerPrefs)

### P1 — เติมเนื้อหา NPC และ mini event

- เพิ่ม NPC ฝ่ายผู้เล่นคนที่สองใน Room03 (เซนะไม่นับ เพราะเป็นผู้เฝ้าประตู ไม่ใช่พวกเดียวกับผู้เล่น)
- กำหนดความสัมพันธ์ระหว่าง NPC
- ทำ event ทะเลาะกันและให้ผู้เล่นเลือกว่าจะรับฟัง ไกล่เกลี่ย หรือเข้าข้าง
- ทำ event ให้ตอบสนองต่อความคืบหน้าด่าน ไม่ใช่สุ่มโดยไม่สนสถานการณ์
- เพิ่มผลระยะยาวของคำตอบ เช่น memory, relationship และการเปิด/ปิดบทสนทนาในอนาคต

### P2 — งาน polish

- เปลี่ยน placeholder art เป็นภาพจริง
- ปรับ animation เดินและพูด
- ปรับ responsive UI สำหรับหลายความละเอียด
- เพิ่มเสียงและ feedback ขณะ interact
- เพิ่ม save/load และเมนูเริ่มเกม

## ขอบเขตงานแนะนำสำหรับคนที่รับช่วงต่อ

Branch แนะนำ: `feature/npc-world-knowledge`

Definition of Done รอบแรก:

- Alice ตอบคำถามเกี่ยวกับภาพ โต๊ะ ลิ้นชัก กุญแจ และประตูโดยยึดข้อมูลด่าน
- Alice ไม่เปิดเผยข้อมูลที่ยังไม่ควรรู้
- เมื่อไม่รู้คำตอบ Alice บอกว่าไม่รู้โดยไม่แต่งข้อมูล
- คำตอบเปลี่ยนตาม flag และ inventory ที่ผู้เล่นทำได้จริง
- มี test/debug scenario ครบห้าช่วง: เริ่มเกม, ตรวจภาพ, พบโน้ต, ได้กุญแจ, เปิดประตู
- local fallback ยังทำงานเมื่อไม่ใส่ API key

ไฟล์หลักที่ต้องเริ่มอ่าน:

- `Assets/Scripts/AI/AiDialogueGenerator.cs`
- `Assets/Scripts/Core/GameState.cs`
- `Assets/Scripts/DialogueManager.cs`
- `Assets/Scripts/Interaction/InteractionSystem.cs`
- `Assets/Scripts/Interaction/InteractionData.cs`
- `Assets/Data/Interactions/`
- `Assets/Data/Dialogue/`
- `Assets/Data/Events/`

## สถานะ Git ก่อนส่งขึ้น GitHub

- branch ปัจจุบันคือ `main`
- มี commit ตั้งต้นเพียงหนึ่ง commit: `chore: snapshot Unity prototype before refactor`
- ยังไม่มี Git remote
- working tree มีการแก้ เพิ่ม และลบไฟล์จำนวนมาก จึงต้อง review ก่อน commit
- การลบ `Assets/TextMesh Pro/Examples & Extras/` เป็นการลดไฟล์ตัวอย่างที่ไม่ใช้ แต่ควรยืนยันว่าไม่มี reference ขาดก่อน commit
- ห้าม commit API key, `Library/`, `Temp/`, `Logs/` หรือ `UserSettings/`

ขั้นส่งขึ้น GitHub หลังทดสอบ Scene ผ่าน:

1. ตรวจ Missing Script/สีชมพู/asset reference ที่หายภายใน Unity
2. ตรวจ diff และยืนยันรายการลบไฟล์ตัวอย่าง
3. commit สถานะต้นแบบที่เล่นได้เป็น baseline
4. สร้าง GitHub repository และเพิ่ม `origin`
5. push `main`
6. ให้ผู้รับช่วงสร้าง branch `feature/npc-world-knowledge` จาก `main`
7. ทำงานผ่าน pull request เพื่อให้ review การเปลี่ยน prompt, schema และข้อมูลด่านได้

ไม่ควรให้เพื่อนเริ่มจาก working tree ที่ยังไม่ commit เพราะจะไม่สามารถดึงสถานะปัจจุบันที่เห็นในเครื่องนี้จาก GitHub ได้
