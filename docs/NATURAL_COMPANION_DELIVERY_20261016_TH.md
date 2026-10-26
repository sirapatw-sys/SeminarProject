# ปรับการชวนสำรวจและอ่านหลักฐาน — 16 ตุลาคม 2026

ฐานก่อนทำ: f9653fe บน codex/npc-cothinking; ก่อนเริ่ม fetch แล้ว ahead 11 / behind 0 เทียบ origin/develop
รอบนี้ไม่ push/PR/merge ไม่ติดตั้ง local model และไม่ใช้ API key/โควตาจริง

## สิ่งที่แก้

- เพิ่ม CompanionNaturalRequests เป็น adapter กลาง ใช้ข้อมูล factId/title/evidenceAliases จาก RoomKnowledgeData ไม่เพิ่มสคริปต์เฉพาะโน้ตหรือ Alice
- คำชวน “มาช่วยดูด้วยกันหน่อย” เข้าสู่ Follow ที่ controller เดิม ตรวจระยะ/ห้อง/สถานะ/modal ตามเดิม และบอกว่าจะเริ่มเดินหลังปิดบทคุย
- คำขออ่านหลักฐานที่พบแล้ว เช่น “ช่วยอ่านโน้ตให้หน่อย” แสดง statement จริงผ่าน EvidenceSharing แม้ความสัมพันธ์ต่ำ ไม่ตีความหรือแถมคำตอบปริศนา
- การขอให้อ่านถือเป็นการยื่นหลักฐานที่ค้นพบให้ NPC; NPC เรียนรู้ fact ตามระบบแบ่งปันเดิม รางวัลครั้งแรกใช้กฎเดิม อ่าน/แบ่งปันซ้ำไม่เพิ่มซ้ำ
- อ่านกับตีความแยกกัน: คำถามรหัส/เฉลย/วิธี/อ่านย้อนทิศยังไม่ถูกยกเว้น hint policy; forbidden/unrevealed/answer/guidance facts ไม่เข้ารายการอ่าน
- เพิ่มบริบทหลักฐานสำหรับข้อความถัดไป การพูดชื่ออย่างเดียวไม่สอน NPC; “อันนี้” ใช้ focus ที่ถูกต้องหรือหลักฐานชิ้นเดียว ถ้ามีหลายชิ้นถามชื่อกลับ
- คำตอบชื่อสั้นใช้ได้เฉพาะหลังเกมถาม และต้องเป็นหนึ่งในหลักฐานที่เสนอจริง ตรวจเงื่อนไขปัจจุบันซ้ำ
- ล้างบริบทเมื่อเปลี่ยนเรื่อง ปฏิเสธ ปิดคุย เปลี่ยน task ปิด actor รีเซ็ต/restore หรือเปลี่ยนห้อง รวมถึงไปแล้วกลับห้องเดิมผ่าน SceneRevision แบบไม่บันทึกลง save
- ชื่อเต็มที่ยาวกว่าชนะ alias ที่ฝังอยู่ เช่น ลูกศรบนขอบกระดาษ ไม่ถูกสับสนกับกระดาษโน้ต; alias เท่ากันหลาย fact ให้ถามกลับ
- เพิ่ม closed companionAction menu: inspect_together / read:<factId> / show:<factId>; menu มีเฉพาะชื่อ/ID หลักฐานที่ player พบแล้ว ไม่มี statement ที่ NPC ยังไม่รู้
- คำชวนบางรูปแบบให้ provider จำลองเสนอ inspect_together แล้วเกมตรวจ consent และผลจริง คำพูดอ้างอิง/สมมติ/ปฏิเสธ/ชื่อเปล่าไม่อนุญาต action
- คำสั่งหยุดยังผ่าน local trusted path ไม่รอ AI; การอ่านไม่ยกเลิกงานเดินตามเดิม

## ขอบเขต

การอ่านในที่นี้เป็นข้อความ ไม่ได้เพิ่ม TTS/เสียงพูด ไม่ได้เพิ่มการเดินไปยังวัตถุใดก็ได้หรือระบบตีความปริศนาใหม่
ยังเป็น adapter ภาษาแบบมีขอบเขต ไม่รับประกันทุกสำนวน และไม่ขยายการแยกคำสั่งหลายช่วง/หลาย comma
ไม่มีผลคุณภาพจาก KKU/model จริงหรือ manual Windows visual QA การคอมไพล์/ชุดจำลองไม่แทนผลดังกล่าว
การเพิ่ม local server/model ยังอยู่นอกขอบเขตรอบนี้; รุ่นเปิด AI ใช้ provider configuration เดิม

## หลักฐานเริ่มต้น

ก่อนแก้ทดสอบจริงใน Unity สองกรณีล้มเหลว 2/2:
- NaturalInvitationStartsFollowingAndExplainsWhenMovementBegins: NPC ยัง Waiting
- ReadingDiscoveredEvidenceIsNotLockedBehindRelationshipOrPuzzleHints: ได้บททั่วไป ไม่ใช่ข้อความโน้ต

ไฟล์ Logs/NaturalCompanion_20261016/Before.xml และ Before.log
รอบตรวจเพิ่มพบและแก้ room round-trip context, alias collision, การแย่ง search command และ figurative reading แล้ว
BoundaryAfter.xml ผ่าน 49/49 ก่อนเพิ่ม live-range/task-preservation regression สุดท้าย

## วิธีลอง

ดู [คู่มือเดโมเปิด AI](AI_ENABLED_DEMO_TH.md#ลองคุยและสำรวจในห้องแรก)
แตก ZIP ทั้งหมดก่อนเปิด exe, ใช้ New Game สำหรับ flow ทดสอบ และใส่ key/model เอง การทดสอบการเชื่อมต่อ/บทคุย provider ใช้โควตาบัญชีผู้เล่น
คำชวน/อ่านที่ local เข้าใจจะไม่เรียก API แม้เปิด AI อยู่ เป็นพฤติกรรมตั้งใจเพื่อให้ action และข้อความหลักฐานตรงกัน

## ผลตรวจสุดท้ายและ build

ผล Unity 2021.3.16f1 จาก source รุ่นสุดท้าย:

| ชุดตรวจ | ผ่าน | ล้มเหลว | ข้าม |
| --- | ---: | ---: | ---: |
| EditModeFinal | 1281/1281 | 0 | 0 |
| PlayModeFinal | 294/294 | 0 | 0 |

เพิ่ม 15 PlayMode regression จากฐาน 279 กรณี รวม fixture CompanionClarificationPlayTests เป็น 24 กรณี
ไฟล์อยู่ใต้ Logs/NaturalCompanion_20261016/; test provider เป็นจำลอง ไม่มี real API evaluation
EditModeFinal.xml SHA256: 2521787D989CA11C2884DE5972A01E4890AAB468EF19EF758397805C55E62177
PlayModeFinal.xml SHA256: C81ADC57A04B191146CAE3C6E3417BEB57179A56FAF3F6F7A0FF51F5F68AA041

## เดโมรุ่นนี้

- Build.log: AI-enabled demo build: Succeeded, errors=0
- Output: Logs/CompanionAiBuilds/20261016-123605-780856/
- ZIP: Logs/CompanionAiBuilds/MysteryGame-AI-Enabled-NaturalCompanion-20261016-123605.zip
- ZIP: 74,188,957 bytes, 179 entries; SHA256 AA795D6F7F1AEC3E272DE6B337C95724C2200A43E348C2352576DD2251E5096D
- ตรวจ compiled MysteryGame.Runtime.dll ด้วย Cecil: มี CompanionNaturalRequests และ Bootstrap แบบปกติที่เริ่ม Debug.get_isDebugBuild ไม่ใช่ forced-offline build
- ZIP มี exe, UnityPlayer, Mono runtime, MysteryGame_Data, README_AI.txt, README_AI_TH.md และ NATURAL_COMPANION_NOTES_TH.md ครบ
- ไม่รวม UserSettings/api_keys.json/*api_key.txt/BurstDebugInformation_DoNotShip
- Source/tests/docs เก็บใน branch; build และผล XML เป็น local artifacts ใต้ Logs ไม่เข้า Git
- ยังไม่ได้เปิดเล่น Windows ด้วยคนหรือทดสอบ provider จริง การใช้คีย์และทดสอบจริงต้องทำในรุ่น AI-Enabled โดยผู้เล่นเอง
