# AI Mystery Game — สถานะงานและแผนส่งต่อ

เอกสารนี้ใช้เป็นจุดเริ่มต้นสำหรับสมาชิกที่ clone/pull โปรเจกต์ไปทำต่อ รายการงานที่ยังค้างอยู่แยกไว้ที่ [REMAINING_WORK_TH.md](REMAINING_WORK_TH.md)

## เป้าหมายของต้นแบบ

เกม escape room แบบ 2D top-down ที่ผู้เล่นสำรวจห้อง แก้ลำดับปริศนา และคุยกับ NPC ได้ทั้งผ่านตัวเลือกและการพิมพ์ข้อความ NPC มีความสัมพันธ์กับผู้เล่นและกับ NPC ด้วยกันเอง มีความทรงจำ ความลับ อารมณ์ และส่งสัญญาณชวนคุยเพื่อเริ่ม mini event ได้

หลักสำคัญของระบบ AI:

- ข้อเท็จจริงของด่าน คำใบ้ และวิธีผ่านต้องมาจากข้อมูลที่ผู้พัฒนาเขียนไว้
- AI ใช้สำหรับเรียบเรียงบทพูด บุคลิก อารมณ์ และความหลากหลายของประโยค
- AI ห้ามเป็นผู้คิดกฎปริศนา ไอเท็ม หรือวิธีผ่านขึ้นมาเอง
- หาก AI ใช้งานไม่ได้ เกมต้องเล่นต่อได้ด้วยบทสนทนาสำรอง
- **ตัวละครและห้องเป็นข้อมูลทั้งหมด** เพิ่ม NPC หรือห้องใหม่ได้โดยไม่ต้องแก้โค้ด

## การเล่นและฉาก

- สามฉาก: `Room01` (ห้องทำงานธรรมดา), `Room02` (หอสมุดเก่า) และ `Room03` (ห้องรับแขกร้างที่มีผี) — บรรยากาศไล่จากธรรมดา → ลึกลับ → สยองขวัญ
- เปิดเกมที่ `Room01.unity` จะเจอ **เมนูเริ่มเกม** (`TitleMenu`) ก่อน: เริ่มเกมใหม่ / เล่นต่อ / ตั้งค่า AI / ออกจากเกม
  - "เริ่มเกมใหม่" เล่น `IntroSequence` แล้วเข้าห้อง
  - เปิด Room02/03 ตรงๆ จาก editor จะไม่เห็นเมนู เล่นห้องนั้นได้ทันทีเหมือนเดิม
- เดินด้วย WASD/ลูกศร กด E เพื่อสำรวจหรือคุย F10 เปิดตั้งค่า AI
- **ปุ่ม E ผ่าน `InteractionFocus` ที่เดียว:** `ObjectInteraction`, `NPCInteraction`, `SenaInteraction` เป็น `IFocusable` แค่แจ้งตอนผู้เล่นเข้า/ออกพื้นที่ ตัวกลางเลือกชิ้นที่ใกล้เท้าผู้เล่นที่สุด แสดงป้ายของชิ้นนั้นและส่ง E ให้ชิ้นนั้นชิ้นเดียว ของใหม่ที่กด E ได้ให้ implement `IFocusable` แทนการอ่าน `Input` เอง
- **สมุดบันทึก (J):** `JournalUI` แสดงทุกข้อความที่ผู้เล่นอ่านเจอ (ข้อความจากการสำรวจที่สำเร็จ + การ์ดไอเท็มที่มีคำอธิบาย) แยกตามห้อง ห้องปัจจุบันขึ้นก่อน ข้อมูลอยู่ใน `GameState.GetJournal()` และบันทึกลง save (`StateSnapshot.Journal`) ประตูที่พาไปห้องอื่นไม่ถูกจด
- **บันทึกเกม:** บันทึกอัตโนมัติทุกครั้งที่เข้าห้องใหม่ กด F5 บันทึกเอง F9 โหลด (`SaveSystem` เก็บ snapshot ทั้งหมดของ `GameState` ไว้ที่ `Application.persistentDataPath/save.json`)
- **เสียงของห้องเป็นข้อมูล:** `RoomKnowledgeData.sounds` คือรายการเสียงวน (คลิป + ความดัง) ของแต่ละห้อง `SfxPlayer` crossfade ให้เองเมื่อเปลี่ยนห้อง เสียงที่ห้องใช้ร่วมกัน (ฝน) ไม่เริ่มใหม่ แค่ค่อยๆ ปรับความดัง
  - Room01: `Room01 BGM 1` + ฝนเบามาก / Room02: `Room02 BGM 1` + `Grandpa Clock 1` เบาๆ + ฝนเบามาก / Room03: ฝนเบา + เสียงเบสตุบๆ ช้าๆ ดังคงที่ และเสียงสุ่มในห้อง (เท้า, ไม้ลั่น, เคาะ, ลากของ) ที่สังเคราะห์เอง (`hauntedDrone`) NPC ที่มี `startleLines` ใน profile (ตอนนี้มีแค่สเตล) จะสะดุ้งกับเสียงสุ่มเป็นบางครั้ง
  - เงียบตอนอยู่หน้าเมนูเริ่ม, เบาระหว่างบทนำ, ดังเต็มเมื่อเข้าห้อง
  - `InteractionData.successClip` = ดนตรีที่เล่นหนึ่งครั้งเมื่อทำสำเร็จ (กล่องดนตรีใน Room03) เสียงห้องจะหรี่ลงระหว่างนั้น
  - ไฟล์เสียงอยู่ที่ `Assets/Audio/` ไฟล์ยาว (ฝน, BGM) ตั้งเป็น Streaming
  - เสียงสั้นตอนกด E / ได้ไอเท็ม ฯลฯ ยังสังเคราะห์เอง ถ้าจะใช้ไฟล์จริง วางที่ `Assets/Resources/Audio/<ชื่อ Cue>` เช่น `Audio/Item.wav`
- **UI หลายความละเอียด:** Canvas ใช้ `ScaleWithScreenSize` 1920x1080 match 0.5 ส่วนหน้าต่าง IMGUI ทั้งหมด (ตั้งค่า AI, ไอเท็ม, keypad, intro, transition, title) ย่อ/ขยายผ่าน `UiScale`

### ลำดับผ่านด่าน Room01

1. ตรวจภาพวาด → `inspected_painting`
2. ตรวจโต๊ะ → ต้องมี `inspected_painting` → `found_note`
3. เปิดลิ้นชัก (รหัส 4592) → ต้องมี `found_note` → item `key`
4. เปิดประตู → ต้องมี `key` → `door_unlocked` แล้วเปลี่ยนฉากไป Room02

### ลำดับผ่านด่าน Room02 (หอสมุดเก่า)

1. คุยกับเซนะ → `sena_wants_tome`
2. อ่านสมุดทะเบียนบนโต๊ะอ่านหนังสือ → `read_ledger`
3. หยิบจารึกจากตู้ชั้นล่างสุด → item `tome`
4. คุยกับเซนะขณะถือ `tome` → `sena_offering_given` นางเอ่ยปริศนา
5. พิมพ์ตอบ `พรุ่งนี้` / `Tomorrow` → `sena_passed`
6. สำรวจประตูดวงดาว → ไป Room03

เซนะมี mini event ของตัวเองสองอัน: เยาะเย้ยเมื่อผู้เล่นหาของถวายนานเกินไป และทวนปริศนาเมื่อผู้เล่นยังตอบไม่ได้ (ไม่มีอันไหนใบ้)

### ลำดับผ่านด่าน Room03 (ห้องรับแขกร้าง)

ตั้งใจให้ง่ายกว่า Room02: **ไม่มีการพิมพ์ตอบปริศนา** ทุกขั้นมีคำใบ้อยู่ในข้อความของวัตถุเองที่ชี้ไปขั้นถัดไป

1. ดูกระจกบานใหญ่ (ที่รอยเท้าเปียกเริ่มต้น) → `saw_mirror_message` ข้อความ "เพลงของหนูหายไป..."
2. ดูกล่องดนตรีบนโต๊ะเครื่องแป้ง (ที่รอยเท้าไปหยุด) → `music_box_needs_key` รูไขลานมีเขม่าเหมือนในเตาผิง
3. คุ้ยเตาผิง → item `winding_key`
4. กลับไปไขกล่องดนตรี (object เดิม ใช้ `followUpInteraction`) → `ghost_lullaby_played`, `room03_door_unlocked` ผีสงบ
5. เปิดประตูทางออก → จบเดโมบทที่ 1

วัตถุประกอบบรรยากาศ (ไม่บังคับ): ภาพวาดเด็กหญิง "ลิลี่", นาฬิกาที่หยุดตีสาม, เก้าอี้โยกที่โยกเอง, โซฟาคลุมผ้า

### ฉากและการชนกับเฟอร์นิเจอร์

- ภาพห้องมาจาก background ภาพเดียวที่ `RoomVisualController` สร้างขึ้น SpriteRenderer ของ prop ใต้ root `Environment` / `Interactables` ถูกปิดตอนรันไทม์
- **background ของแต่ละห้องอยู่ใน `RoomKnowledgeData` ของห้องนั้น** (`Assets/Resources/Knowledge/Rooms/<roomId>.asset` ช่อง `background`) ช่อง `room0XBackground` บน component ในแต่ละ scene เป็นแค่ fallback และต้องเหมือนกันทุก scene
- **การชนใช้ "เท้า"**: collider ของผู้เล่นเป็นกล่องแบนมุมมน 0.6x0.3 ที่ฝ่าเท้า (ไม่ใช่กลางลำตัวเหมือนเดิม) และไม่มีแรงเสียดทาน จึงไถลไปตามผนังได้ กำแพงวางชิดขอบพื้นที่วาดไว้ `Obstacle_*` คือรอยที่เฟอร์นิเจอร์วางบนพื้น trigger ของวัตถุและวงคุยของ NPC ก็อยู่ระดับเท้าด้วย
- ภาพพื้นหลังมีขนาด world คงที่ 17.78x10 ทุกห้อง กล้องปรับ orthographic size ตามสัดส่วนจอ (`RoomVisualController.FitCamera`) **ห้ามกลับไปขยายภาพให้เต็มจอ** เพราะภาพจะเลื่อนออกจาก collider
- NPC ไม่มี collider ตัน ผู้เล่นเดินทะลุได้ มีแค่วงคุยรัศมี ~1 หน่วยที่เท้า
- การแปลงพิกัดภาพ 1920x1080 เป็น world: `world_x = (px - 960) / 108`, `world_y = (540 - py) / 108`
- ภาพพื้นหลังทั้งสามห้องสร้างด้วยสคริปต์ใน `tools/art/` (ดู README ในโฟลเดอร์นั้น): Room01/02 ประกอบจาก asset pack ใน `CopyPasteAssets/` (ส่วนใหญ่ `Assets Source 1`) ขยาย 4 เท่าแล้วจัดแสงแบบเดียวกับ Room03, Room03 วาดด้วยโค้ดทั้งหมด ทุกห้องมืดและชวนอึดอัด แต่มีผีแค่ Room03
- ถ้าย้ายเฟอร์นิเจอร์ในภาพ ต้องย้าย collider ตาม — สคริปต์ส่ง `*_layout.json` ที่มีพิกัดของทุกชิ้นออกมาให้

## ระบบ NPC

### ข้อมูลตัวละคร (`NpcProfileData`)

อยู่ที่ `Assets/Resources/Knowledge/Npcs/<npcId>.asset` ชื่อไฟล์ต้องตรงกับ `DialogueData.speakerId`

| ช่อง | ใช้ทำอะไร |
|---|---|
| `persona` / `speechStyle` / `personalGoal` | บุคลิกที่ส่งเข้า prompt แทนข้อความที่เคยฮาร์ดโค้ดในโค้ด |
| `portrait`, `initialRelationship` | ภาพใน dialogue และค่าความสัมพันธ์เริ่มต้นกับผู้เล่น |
| `emotions[]` | หน้าในกล่องอารมณ์ (emotion box) พร้อมความหมายให้ AI เลือก |
| `backstory[]` | เรื่องของตัวเองที่เล่าได้เสมอเมื่อถูกถาม |
| `secrets[]` | ความลับที่เปิดเมื่อเงื่อนไขเป็นจริง (ใช้ `KnowledgeGrant` เดิม) เช่น ความสัมพันธ์ ≥ 65 |
| `situationalNotes[]` | คำสั่งเพิ่มเติมที่ใช้เฉพาะบางสถานการณ์ เช่น สถานะของเซนะแต่ละขั้น หรือผลของการทะเลาะ |
| `bonds[]` | ความสัมพันธ์กับ NPC ตัวอื่น ค่าเริ่มต้น + มุมมองต่อกัน |
| `knownFactIds` / `learnedFacts` / `forbiddenFactIds` | สิทธิ์รู้ข้อมูลของห้อง |
| `givesHints`, `refuseHintLine`, `tones[]` | ระดับคำใบ้ตามความสัมพันธ์ |
| `fallbackReplies[]`, `neutralReplies[]` | **บทตอบสำรองตอนไม่มี AI** ไล่จากบนลงล่าง ตรงกฎแรกชนะ จับจากเจตนา (`PlayerIntent`), คำสำคัญ และเงื่อนไข |
| `returnGreetings[]` | ประโยคเปิดเมื่อกลับมาคุยอีกครั้ง |

`DialogueManager` ไม่มีบทพูดของตัวละครใดๆ ในโค้ดแล้ว การเพิ่ม NPC ใหม่ = สร้างไฟล์ profile + DialogueData + วางใน scene

### ประวัติสนทนาและความทรงจำ

- `GameState` เก็บ log บทสนทนาแยกต่อ NPC (สูงสุด 40 บรรทัด) ทั้งข้อความที่พิมพ์ ตัวเลือกที่เลือก และคำตอบของ NPC
- prompt ส่งท้ายสุด 10 บรรทัด ภายใน 1,400 ตัวอักษร (บรรทัดยาวถูกตัดเหลือ 240 ตัว) และ memory ที่มาจาก event/ตัวเลือกอีกสูงสุด 4 รายการ
- ความลับที่ NPC เล่าแล้ว (AI อ้าง factId ของความลับ) จะตั้ง flag `secret.<npc>.<id>.told` และบันทึกเป็น memory

### กล่องอารมณ์และบทพูดหลายคน

- บรรทัดใน `DialogueData.lines` / `responseText` / `returnGreetings` ขึ้นต้นด้วยแท็กได้:
  - `[Stelle]` เปลี่ยนชื่อและภาพผู้พูดเป็น Stelle สำหรับบรรทัดนั้น
  - `[:shock]` เด้งกล่องอารมณ์หน้า shock ของผู้พูด
  - `[Stelle:cry]` ทั้งสองอย่าง
- AI ส่งช่อง `emotion` กลับมาได้ ถ้าตรงกับ `emotions[]` ของ NPC กล่องอารมณ์จะเด้งขึ้น
- สเตลใช้ระบบนี้ตั้งแต่คุยครั้งแรก: shock / flustered / cry / blank (blank = อึ้งหรือน้อยใจ) ภาพอยู่ที่ `Assets/Art/Portraits/Emotions/` พื้นหลังโปร่งใส กล่องอารมณ์ไม่มีกรอบ

### Mini event

- trigger: need, emotion, เวลาในห้อง, สุ่ม, ความขัดแย้งระหว่าง NPC และ **`RoomProgress`** (ยิงเมื่อขั้นปัจจุบันของห้องเป็นขั้นที่กำหนด)
- `storyBeat` = ยิงทันทีที่เงื่อนไขครบโดยไม่สุ่ม และถ้าผู้เล่นเลยจุดนั้นไปแล้วเครื่องหมาย "!" จะหายเอง
- `autoStart` = เล่นเองทันทีที่ไม่มีบทสนทนา/popup อื่นเปิดอยู่ (ใช้กับ story beat ทั้งหมดของ Room03)
- `DialogueData.firstMeetingChoicesOnly` = ตัวเลือกของบทนั้นขึ้นแค่ครั้งแรก ครั้งต่อไปคุยพิมพ์อิสระอย่างเดียว
- ตัวเลือกได้สูงสุด 4 ข้อ
- event ที่มีอยู่:

| Event | NPC | เกิดเมื่อ | ผลระยะยาว |
|---|---|---|---|
| Alice_Thirst / Alice_Homesick | Alice | need/emotion สูง | ความสัมพันธ์ + memory |
| Sena_Impatient / Sena_RiddleTaunt | Sena | อยู่ใน Room02 นานโดยยังไม่คืบหน้า | ความสัมพันธ์ + memory |
| Stelle_Scare | Stelle | เพิ่งอ่านข้อความบนกระจก | ปลอบ = สเตลไว้ใจเร็วขึ้น |
| Room03_Quarrel | Rina + Stelle | ดูกล่องดนตรีแล้วแต่ยังไม่เจอที่ไขลาน และ Rina–Stelle ≤ 45 — เล่นเองทันที | 4 ทาง: รับฟัง / ไกล่เกลี่ย / เข้าข้างรินะ / เข้าข้างสเตล — เปลี่ยน flag, ความสัมพันธ์ทั้งกับผู้เล่นและระหว่างกัน, situational notes และ event ถัดไป |
| Stelle_Relief / Stelle_Hurt | Stelle | ผีสงบแล้ว แยกตามว่าเคยเข้าข้างรินะหรือไม่ | ขอโทษได้ `stelle_forgave` |

### ตัวละคร Room03

- **รินะ (Rina)** — kuudere สายเท่: ใจเย็น นิ่ง พูดตรง มุกประชดแห้งๆ พูดพอดีไม่น้อยไม่มาก ติดอยู่ที่นี่มาสามวัน ห่วงสเตลแบบพี่สาวแต่พูดแรงบ่อย ความลับ: จริงๆ กลัวผี (≥65) และเริ่มลืมนามสกุลตัวเอง (≥75) ให้คำใบ้ได้
- **สเตล (Stelle)** — ขี้อาย ขี้กลัว ตกใจแล้วโวยวายนิดๆ ตั้งแต่เจอกันครั้งแรก ตอนยังไม่สนิทจะพูดติดอ่าง ขอโทษบ่อย และยังไม่ช่วยคิด (ความสัมพันธ์เริ่ม 35, ≤45 = ยังไม่สนิท) ความลับ: รู้จักเพลงกล่อมเด็ก (≥55) และเคยเห็นเด็กผู้หญิงในกระจก (≥65)
- **Alice** ตามผู้เล่นเข้ามา รู้ว่าห้องนี้ต่างจากสองห้องแรกและกลัวแต่ปากแข็ง

### NPC World Knowledge

- `RoomKnowledgeData` (`facts[]`, `steps[]` พร้อมคำใบ้สามระดับ และ `interactionId` ที่ผูกกับ `InteractionData`)
- `NpcKnowledgeContextBuilder` คัดเฉพาะสิ่งที่ NPC รู้และพูดได้ + ขั้นถัดไป + คำใบ้แบบ deterministic + ส่วนตัวละคร + ประวัติสนทนา
- `AiDialogueGenerator.BuildReplyPrompt` สร้าง prompt จากข้อมูลทั้งหมด ไม่มีข้อความเฉพาะห้องหรือเฉพาะตัวละครในโค้ดแล้ว
- คำตอบที่อ้าง factId นอกสิทธิ์ (รวมความลับที่ยังล็อก) ถูกทิ้งและใช้บทสำรองแทน
- ทุกห้องมีไฟล์ knowledge ครบแล้ว (Room01–03)

## AI provider

- รองรับ OpenAI Responses, KKU IntelSphere (`https://gen.ai.kku.ac.th/api/v1/chat/completions`), Gemini และ custom OpenAI-compatible
- โหลดรายชื่อโมเดลจาก `/models`, จำ provider/endpoint/model ผ่าน PlayerPrefs, API key อยู่ในหน่วยความจำเท่านั้น
- key หลักอ่านจาก env (`KKU_API_KEY`) → `UserSettings/kku_api_key.txt` → `api_keys.json` (ทั้งหมดอยู่นอก git)
- **key สำรอง** ชื่อเดียวกันต่อท้าย `_BACKUP` / `_backup.txt` ใช้เฉพาะเมื่อ key หลักตอบว่าโควตา/โทเค็น/เครดิต/งบหมด (`AiProviderDiagnostics.IsQuotaUsedUp`) ส่งคำขอเดิมซ้ำครั้งเดียวด้วย key สำรองแล้วใช้ต่อจนปิดเกม หน้าตั้งค่าจะบอกว่ากำลังใช้ key สำรอง
- `AiResponseParser` แยกออกมาและ **ไม่ throw** เมื่อเจอ JSON ผิดรูป + `DialogueManager` มี timeout 30 วินาที บทสนทนาจึงไม่ค้างที่ "กำลังคิด..." อีก
- หน้าตั้งค่าแสดงโควตาคงเหลือ ถ้า response มี header `x-ratelimit-remaining-*` หรือช่อง quota ใน body (ถ้าผู้ให้บริการไม่ส่งมาจะบอกตามจริงว่าไม่มีข้อมูล)

## การทดสอบ

EditMode tests ที่ `Assets/Tests/Editor/` เปิดผ่าน `Window > General > Test Runner`

| ไฟล์ | ครอบคลุม |
|---|---|
| `NpcKnowledgeTests` | ลำดับขั้นของทั้งสามห้อง การกันข้อมูลรั่ว เซนะไม่ใบ้ สเตลไม่ใบ้ตอนยังไม่สนิท |
| `AiResponseParserTests` | API ล้มเหลว / JSON ผิดรูป / body เป็น HTML / ค่าเกินช่วง ต้องได้ null โดยไม่ throw, อ่านโควตา |
| `InteractionConsistencyTests` | เงื่อนไขใน `Assets/Data/Interactions/` ตรงกับ `steps[]` ทั้งสองทิศ, การทำ interaction ทำให้ขั้นเสร็จจริง, ทุกห้องมีประตูเข้า, event มี dialogue ถูกรูป |
| `JournalTests` | สมุดบันทึกเก็บตามห้อง อ่านซ้ำแล้วอัปเดตที่เดิม รอด save/load และ save เก่าที่ไม่มีสมุดยังโหลดได้ |
| `AiBackupKeyTests` | key สำรองใช้เฉพาะเมื่อ key หลักโควตาหมด ข้อผิดพลาดอื่นไม่นับเป็นโควตาหมด |
| `NpcConversationTests` | ประวัติสนทนาและงบตัวอักษร, prompt มาจาก profile, บทสำรองจากข้อมูล, ความลับ, ความสัมพันธ์ระหว่าง NPC, event ทะเลาะและผลต่อเนื่อง, save/load |

tests ใช้ `GameStateFixture` ซึ่งตั้ง `GameState.UseForTests(...)` เอง เพราะ Unity ไม่เรียก `Awake` ใน Edit Mode

โปรเจกต์แบ่งเป็นสอง assembly: `MysteryGame.Runtime` (`Assets/Scripts/`) และ `MysteryGame.Tests.EditMode` (`Assets/Tests/Editor/`)

## ไฟล์หลักที่ควรอ่านก่อน

- `Assets/Scripts/AI/AiDialogueGenerator.cs`, `AiResponseParser.cs`
- `Assets/Scripts/Core/GameState.cs`, `SaveSystem.cs`
- `Assets/Scripts/DialogueManager.cs`
- `Assets/Scripts/Knowledge/` ทั้งโฟลเดอร์
- `Assets/Scripts/Events/`
- `Assets/Resources/Knowledge/` (ข้อมูลห้องและตัวละคร)
- `Assets/Data/` (interaction, dialogue, event)

## Git

- remote: `origin` → `github.com/sirapatw-sys/SeminarProject` ทำงานบน `develop`
- `CopyPasteAssets/` (ต้นฉบับภาพตัวละครและชีทท่าเดิน) **commit ขึ้น repo** เพื่อให้เพื่อนตัดเฟรมใหม่เองได้
- `scratch/` (ภาพทดลอง) อยู่ใน `.gitignore`
- ห้าม commit API key (`api_keys.json`, `*_api_key.txt` อยู่ใน `.gitignore` แล้ว), `Library/`, `Temp/`, `Logs/`, `UserSettings/`
