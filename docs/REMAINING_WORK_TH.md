# งานที่ยังไม่ได้ทำ

อัปเดต **23 กันยายน 2026** หลังรอบที่ทำตามรายการเดิมจนครบ ภาพรวมของระบบอยู่ใน [PROJECT_HANDOFF_TH.md](PROJECT_HANDOFF_TH.md)

---

## ต้องทำก่อน: เปิด Unity ตรวจหนึ่งรอบ

รอบนี้ทำงานนอก Unity ทั้งหมด (Unity ในเครื่องรันแบบ batchmode ไม่ได้เพราะต้องล็อกอิน license) สิ่งที่ตรวจแล้ว:

- คอมไพล์ `MysteryGame.Runtime` และ `MysteryGame.Tests.EditMode` ผ่านด้วย Roslyn ของ Unity 2021.3.16f1 ไม่มี error/warning
- ทุก scene และ asset parse เป็น YAML ได้ ไม่มี fileID ซ้ำ ไม่มี reference ภายในที่ขาด และทุก GUID ที่อ้างถึงหาไฟล์เจอ (รวมของ package) — แปลว่าการลบ `TextMesh Pro/Examples & Extras` ไม่ได้ทิ้ง reference ค้างไว้
- เงื่อนไขของ interaction ตรงกับ `steps[]` ทุกห้อง (ตรวจด้วยสคริปต์เดียวกับ test)
- collider ของ Room03 วางทับภาพแล้ว flood-fill จากจุดเกิด: เดินถึงทุก interactable

ที่ยังต้องทำใน Unity:

1. เปิดโปรเจกต์ ให้ Unity import ภาพใหม่และสร้าง sprite
2. รัน `Window > General > Test Runner > EditMode` ทั้งหมด (**ยังไม่เคยรันจริง**)
3. เล่นตั้งแต่ Room01 จนจบ Room03 ดู Missing Script / สีชมพู / ขนาดตัวละคร Rina–Stelle / ตำแหน่ง "!" เหนือหัวเซนะ
4. เช็กกล่องอารมณ์ของสเตลกับตัวเลือก 4 ข้อตอนทะเลาะว่าไม่ทับกัน

---

## ทำเสร็จในรอบนี้

| รายการเดิม | ผลลัพธ์ |
|---|---|
| P0-1 ส่งประวัติสนทนาหลายข้อความ | log แยกต่อ NPC, ส่ง 10 บรรทัดล่าสุดภายใน 1,400 ตัวอักษร |
| P0-2 ประวัติและความลับใน `NpcProfileData` | `backstory[]`, `secrets[]` (โครง `KnowledgeGrant`), `situationalNotes[]`, `bonds[]` |
| P0-3 ย้าย fallback ออกจาก `DialogueManager` | `fallbackReplies[]`, `neutralReplies[]`, `returnGreetings[]` ในข้อมูล + `NpcOfflineReplies` / prompt ของเซนะกับ Alice ย้ายเป็นข้อมูลด้วย |
| P0-4 `Room03.asset` | สร้างแล้วพร้อมภาพพื้นหลังใหม่ ไม่ยืม `Room02_AssembledRoom.png` อีก |
| P0 tests: API ล้ม / JSON ผิดรูป | `AiResponseParserTests` + แก้บั๊กจริง: `JsonUtility` throw ทำให้แชตค้าง, เพิ่ม timeout |
| P0 tests: Interactions ↔ steps | `InteractionConsistencyTests` (เพิ่มช่อง `PuzzleStep.interactionId`) |
| P1 แสดง quota | อ่านจาก header / body แสดงในหน้าตั้งค่า |
| P1 mini event ของเซนะ | `Sena_Impatient`, `Sena_RiddleTaunt` |
| P1 ความสัมพันธ์ระหว่าง NPC | `bonds[]` + เงื่อนไข `NpcRelationshipAtLeast/AtMost` |
| P1 event ทะเลาะ | `Room03_Quarrel` รับฟัง / ไกล่เกลี่ย / เข้าข้างรินะ / เข้าข้างสเตล |
| P1 event ตามความคืบหน้าด่าน | trigger `RoomProgress` + `storyBeat` |
| P1 ผลระยะยาวของคำตอบ | flag, ความสัมพันธ์สองชั้น, memory, situational notes, event ถัดไปที่แยกตามทางเลือก |
| P1 NPC ฝ่ายผู้เล่นคนที่สองใน Room03 | รินะและสเตล |
| P1 Room03 | ปริศนา 5 ขั้นแบบไม่ต้องพิมพ์ตอบ ธีมสยองขวัญ |
| P2 responsive UI | CanvasScaler match 0.5 + `UiScale` สำหรับ IMGUI |
| P2 เสียงและ feedback | `SfxPlayer` + เสียงตกใจ/ambience ใน Room03 |
| P2 save/load และเมนูเริ่มเกม | `SaveSystem`, `TitleMenu`, F5/F9, ฉากจบเดโม |
| P2 placeholder art | Room03 ได้ภาพพื้นหลัง, Rina/Stelle ได้ภาพในฉาก + portrait, ไอเท็มที่ไขลาน |
| `*_Old.png` | ตรวจแล้วไม่มีอะไรอ้างถึง ลบแล้ว |
| Git | `CopyPasteAssets/` commit ขึ้นไป, `scratch/` ใส่ `.gitignore` |

---

## ยังค้าง

### ท่าเดิน (พักไว้ตามที่ตกลง)

ยังไม่ทำโดยตั้งใจ — รอ gen ชีทใหม่ก่อน

- ชีทของ Zero จังหวะขาไม่สลับ หัวสั่น (วัดแล้วต่างกันแค่ 1.10–1.24 เท่า ควรเป็น 3–5 เท่า) ต้อง gen ใหม่โดยล็อกจังหวะขา contact-pass-down-pass และตรึงหัวกับลำตัว
- Alice ยังใช้ภาพนิ่ง `Alice_Chibi.png` ชีท `CopyPasteAssets/.../Alice อลิซ.png` ใช้สคริปต์ตัดตัวเดียวกับ Zero ได้
- ชีทของ Zero มีเงาติดมา Alice ไม่มี ทางที่สะอาดที่สุดคือ gen ใหม่แบบไม่มีเงา
- Rina/Stelle ใช้ภาพนิ่งจากเฟรมแรกของชีทพร้อมการเด้งแบบ procedural (ชีทของสองคนนี้เป็นแนวนอนทิศเดียว ไม่ใช่ 8x4 แบบ Zero)

### ภาพพื้นหลัง Room03

`Room03_HauntedParlor.png` วาดด้วยโค้ดที่ `tools/art/paint_room03.py` (รันซ้ำได้ ผลลัพธ์เหมือนเดิมทุกพิกเซล) โทนและองค์ประกอบเข้ากับ Room01 แต่รายละเอียดยังเรียบกว่าภาพวาดของ Room01/02 ถ้าจะ gen ภาพใหม่ ให้คงตำแหน่งเหล่านี้ (พิกเซลบนภาพ 1920x1080) เพื่อไม่ต้องย้าย collider:

| วัตถุ | x0, y0 – x1, y1 |
|---|---|
| พื้นที่เดินได้ | 150, 300 – 1770, 960 |
| เตาผิง | 250, 70 – 560, 330 |
| หน้าต่าง | 640, 70 – 790, 262 |
| กระจกบานใหญ่ | 870, 62 – 1050, 282 |
| ภาพวาดเด็กหญิง | 1130, 88 – 1270, 238 |
| นาฬิกาตั้งพื้น | 1330, 44 – 1410, 318 |
| ประตูทางออก | 1480, 66 – 1660, 300 |
| โต๊ะเครื่องแป้ง + กล่องดนตรี | 1570, 470 – 1762, 690 |
| เก้าอี้โยก | 590, 360 – 700, 480 |
| ตู้หนังสือแคบ | 158, 372 – 236, 600 |
| โซฟาคลุมผ้า | 175, 700 – 455, 880 |
| โต๊ะข้าง | 470, 790 – 560, 870 |
| หีบของเล่น | 1540, 820 – 1700, 930 |
| เชิงเทียน | 1110, 330 – 1170, 390 และ 310, 560 – 370, 620 |

### เล็กน้อย

- `Assets/Art/Backgrounds/Room02_AssembledRoom.png`, `Room01_2D.png`, `Room01_Study.png`, `Room02_GrandChamber.png` ไม่มีอะไรอ้างถึงแล้ว ลบได้ถ้าไม่ต้องการเก็บไว้เทียบ
- KKU ยังไม่เคยเห็นว่าส่ง header โควตามาหรือไม่ ถ้าไม่ส่ง หน้าตั้งค่าจะบอกตามจริงว่าไม่มีข้อมูล
- ชื่อเกมบนเมนูเริ่ม ("ห้องที่จำใบหน้าเราได้") ตั้งชั่วคราว แก้ได้ที่ `Assets/Scripts/UI/TitleMenu.cs`
