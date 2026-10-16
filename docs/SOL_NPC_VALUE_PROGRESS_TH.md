# ความคืบหน้าแผนคุณค่า NPC
อัปเดตเดโม: มีรุ่น AI-Enabled แยกจาก offline แล้ว พร้อมแก้ข้อความบันทึกตั้งค่า ดู [วิธีเปิด AI จริง](AI_ENABLED_DEMO_TH.md) และ [หลักฐานส่งมอบรุ่น AI](AI_ENABLED_DELIVERY_20261016_TH.md) ชุดล่าสุดผ่าน EditMode 1281/1281 และ PlayMode 279/279 โดยยังไม่ได้ยิง provider จริง

## ล่าสุด — ทำตามแผนคุณค่า วันที่ 16 ตุลาคม 2026

- แก้ A: AI proposal ต้องมีเจตนาขอช่วย/ค้นจากข้อความจริง ตรวจหลังแก้ target ด้วย ชื่อสิ่งของพร้อม “นี่/ด้วย/the” ไม่เริ่มงาน คำขอจริงและคำตอบหลังถามกลับยังทำงาน
- เพิ่ม B รุ่นทดลอง: Alice ชวนทบทวนด้วย ! หลังเห็นการลองลิ้นชักไม่สำเร็จจริง ใช้ MiniEventData/DialogueData เดิม เปิดเมื่อผู้เล่นกดคุยและพิมพ์เอง ไม่มี choice/action/reward จากบทชวน อ่านแล้วไม่ซ้ำ ละเลยไม่มีโทษ และหมดอายุเมื่อปริศนาสำเร็จ
- ผลสุดท้าย: EditMode 1281/1281, PlayMode 277/277; package ในโปรเจกต์แยก EditMode 22/22 และ PlayMode 1/1 ทั้งหมดจำลอง/ออฟไลน์
- หลักฐานใหม่อยู่ใน Logs/ValueDelivery_20261016/Verified{EditMode,PlayMode}.xml, Package{EditMode,PlayMode}.xml และ ReviewEventFinal.xml (2/2)
- เตรียม [คู่มือเดโม](ROOM01_DEMO_RUNBOOK_TH.md), [บทพูด 5 นาที](NPC_PRESENTATION_SCRIPT_TH.md), [หน้าแนะนำโครงการ](NPC_PROJECT_SHOWCASE_TH.md), [ภารกิจผู้พัฒนา](COMPANION_DEVELOPER_TRIAL_TH.md) และ [คู่มือ host integration](../FrameworkPackages/com.mysterygame.companion-reasoning/INTEGRATION.md)
- C ยังรอผู้เล่นจริง; D ผ่านอัตโนมัติและมีชุดทดลองภายใน แต่ยังรอผู้พัฒนาอื่นลองด้วยตนเอง; B ยังรอ visual/audio QA และ feedback ว่าจังหวะใหม่ช่วยหรือรบกวน
- รายละเอียด build/ZIP/fingerprints และขั้นถัดไป: [VALUE_DELIVERY_20261016_TH.md](VALUE_DELIVERY_20261016_TH.md)

## ประวัติรอบ 27c1626

ข้อกล่าวว่าชื่อเปล่าไม่เริ่มงานในบันทึกเดิมด้านล่างมีช่องโหว่ที่ probe ภายหลังพบ 6/8 combinations และได้แก้พร้อม regression ในรอบล่าสุดข้างต้นแล้ว ตัวเลข suite ด้านล่างเป็นผลของรอบเดิม

วันที่ 16 ตุลาคม 2026 — ทำต่อจาก code cebeda8 และแผน 5cd24c7 ใน codex/npc-cothinking

## สิ่งที่ส่งมอบรอบนี้
- P2: ถ้าผู้เล่นขอให้ช่วยจับแต่ยังไม่มีเป้าหมายชัด NPC ถามกลับและรับชื่อสถานีสั้น ๆ ในข้อความถัดไปได้
- ชื่อเปล่าที่ไม่มีคำถามค้างไม่เป็นคำสั่งใหม่ แม้ provider เสนอ help หรือ search ก็ตาม
- เก็บ context เฉพาะ NPC/room/conversation/task identity และ candidate IDs ไม่เก็บลงเซฟ คำถามหมดอายุในข้อความถัดไปหรือเมื่อ close/reset/restore/disable
- ยังตรวจ range/conditions/completion ผ่าน controller เดิมก่อนช่วย และให้ผลหลัง interaction จริง
- P0: เครื่องมือ trace local เปิดจาก Editor menu; เดโม offline เปิด trace ให้อัตโนมัติ บันทึก IDs, route, ผล และเวลา ไม่บันทึกข้อความสนทนา/prompt/key
- P0: Offline Windows development build สร้างสำเร็จ errors=0 ปิดการเรียกบทคุย AI การถอดเสียง และโหลดโมเดล โดยใช้ define NPC_PLAYTEST_BUILD เฉพาะ build นี้
- P1: มี ROOM01_PLAYTEST_PROTOCOL_TH.md, PLAYTEST_RESULTS_TH.md และ tools/Summarize-CompanionPlaytest.ps1 พร้อมใช้ รอเก็บข้อมูลคนจริง

## วิธีลองการถามกลับ
1. เปิด Room01 และเริ่มใหม่ สำรวจภาพวาดเพื่อเปิดโอกาสช่วยโต๊ะ
2. คุยใกล้ Alice ในบริเวณที่ไม่ติดจุดช่วยงานใดจุดหนึ่ง แล้วพิมพ์ “ช่วยจับให้หน่อย”
3. ถ้าไม่มีเป้าหมายจากบริบท เธอจะถามว่าจะช่วยตรงไหนพร้อมชื่อสิ่งที่ช่วยได้ พิมพ์ “ขอบโต๊ะ” หรือ “โต๊ะหน่อยค่ะ”
4. ปิดบทสนทนา รอเธอเดินไปประคอง แล้วไปสำรวจอีกฝั่ง ผู้เล่นเป็นคนพบโน้ตและเธอรู้เฉพาะสิ่งที่เห็นจริง
5. ลองยกเลิกหรือเปลี่ยนเรื่องก่อนตอบชื่อ แล้วพิมพ์ชื่อเปล่าอีกครั้ง ต้องไม่เริ่มงานค้าง

ถ้าอยู่ใกล้สถานีเดียวที่ทำได้อยู่แล้ว คำขอเดิมจะใช้บริบทตำแหน่งช่วยได้ทันที การถามกลับใช้ในกรณีที่ยังไม่รู้เป้าหมาย
ขณะนี้รับคำตอบชื่อ/alias พร้อมคำสุภาพที่กำหนดไว้หนึ่ง turn ยังไม่รับประกันภาษาอิสระทุกรูปแบบ ไม่เพิ่มคำสั่งค้นเฉพาะวัตถุหรือการแก้ประโยคหลายช่วงในรอบนี้

## เดโม
- โฟลเดอร์ local: Logs/CompanionPlaytestBuilds/20261016-055242-4a745d/
- เปิด MysteryGame.exe แล้วเลือก New Game โดยเก็บ MysteryGame_Data และไฟล์ runtime ในโฟลเดอร์เดียวกัน
- ZIP local: Logs/CompanionPlaytestBuilds/MysteryGame-Offline-20261016-055242.zip
- ตรวจ ZIP มีไฟล์ runtime ครบ 211 entries ขนาด 81,899,501 bytes (ประมาณ 78.1 MiB) และไม่รวม BurstDebugInformation_DoNotShip
- ZIP SHA256: CF41A3DADBA06D2355B62764B21A00DA2701A5C9F916ECC9F460F8EAB3098406
- สร้างใหม่ได้ผ่าน Mystery Game > Playtest > Build Offline Windows Demo; ทุกครั้งใช้โฟลเดอร์ใหม่
- Trace ของ build อยู่ใต้ Application.persistentDataPath/Playtests; path จากเกมขึ้นกับ company/product ใน ProjectSettings
- Editor trace: เริ่ม Play แล้วเลือก Mystery Game > Playtest > Start Local Trace; เมื่อจบเลือก Stop Local Trace
- Trace ปกติปิดเป็นค่าเริ่มต้น ตัวเดโม offline บันทึก trace เพราะสร้างมาเพื่อ playtest ให้แจ้งผู้เล่นตาม protocol ก่อนใช้
- การสร้าง build สำเร็จเป็นหลักฐานการคอมไพล์และจัดแพ็กเกจ ยังไม่มีผลเปิดเล่น Windows ด้วยคนหรือ visual/audio QA

## หลักฐาน
ก่อนแก้กรณี MissingTargetThenShortAnswerStartsRealHelpOnlyOnce ล้มเหลว 1/1 เพราะ ActiveStation ยังเป็น null หลังตอบชื่อสั้น:
Logs/CompanionValue_20261016/Before.xml

หลังทำ:
| ชุดตรวจ | ผ่าน | ล้มเหลว | ข้าม |
| --- | ---: | ---: | ---: |
| EditMode ทั้งเกม | 1281/1281 | 0 | 0 |
| PlayMode ทั้งเกม | 274/274 | 0 | 0 |

เพิ่ม 3 Editor cases สำหรับ trace/ขอบเขตออฟไลน์ และ 8 PlayMode cases สำหรับบทสนทนาต่อเนื่อง/การยกเลิก/lifecycle/live conditions/model proposal/trace ไม่ซ้ำ
ผลทั้งหมดใช้ provider จำลอง/ออฟไลน์ ไม่ใช้ key หรือ quota จริง
ตัวสรุป PowerShell ตรวจด้วย SyntheticTrace.jsonl ที่ตั้งค่ารู้คำตอบไว้ ไม่ใช่ข้อมูลผู้เล่น: requests 2, completed 1, cancelled 1, fallback 1, median 2s และ p95 3s ตรงตาม fixture

SHA256:
- FinalEditMode.xml: 660A15E40B7B7D1164D0FD00371791397B038549F1E5A6B5B917E240C12A355C
- FinalPlayMode.xml: 4F373AEFA178E6B6C398CC962842B3C9AA84B24529DA018C1313253B587ED425

ผล XML/log อยู่ใน Logs/CompanionValue_20261016/ ส่วน Build.log ยืนยัน Succeeded errors=0
ผล Logs และ build ZIP เป็น local artifacts ที่ไม่เข้า Git ตัวโค้ด เครื่องมือ และเอกสารทำซ้ำจะติดไปกับสาขา

## สถานะแต่ละขั้น
| ขั้น | สถานะที่พิสูจน์แล้ว | งานที่ยังเหลือ |
| --- | --- | --- |
| P0 เดโมและ baseline | เครื่องมือพร้อม, suite และ Windows build ผ่าน | ทดลองเปิด/เล่นจริง เก็บ baseline trace จากเส้นทางคนเล่นและ visual/audio QA |
| P1 ผู้เล่น 3–5 คน | protocol/แบบบันทึก/ตัวสรุปพร้อม | ยังไม่มีผู้ทดสอบจริง ยังไม่ตัดสินเกณฑ์ 3/5 |
| P2 ถามกลับต่อเนื่อง | ผ่าน automated integration และ safety cases | ลองภาษาที่คนใช้จริงแล้วปรับจากหลักฐาน |
| P3 ชวนทบทวนจากสถานการณ์ | ยังไม่เริ่ม | เลือกเมื่อผล playtest ชี้ว่าจำเป็น |
| P4 การใช้ต่อ | package เดิมยังอยู่ ไม่เปลี่ยนในรอบนี้ | ให้เพื่อนติดตั้งเองและวัดความง่ายจริง |

ขั้นถัดไป: ใช้เดโมกับผู้เล่นใหม่ตาม protocol เก็บสิ่งที่ผู้เล่นทำเองและสิ่งที่ต้องมีคนบอก แล้วเลือกปัญหาที่กระทบความร่วมมือสูงสุดจากผลจริง
ยังไม่ push/PR/merge หรือส่งข้อความให้ทีม
