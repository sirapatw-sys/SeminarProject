# แก้สองประเด็นจากการตรวจ Natural Companion — 16 ตุลาคม 2026

ฐาน: af580ce บน codex/npc-cothinking; ก่อนทำ fetch origin แล้ว ahead 12 / behind 0 เทียบ origin/develop
รอบนี้ไม่ push/PR/merge ไม่ติดตั้ง local AI และไม่เรียก provider จริง

## การแก้ไข

1. แยก local recognition ออกจากการตรวจ model proposal
   - คำขอที่เกมอ่านได้แน่ชัดใช้ local path ตามเดิม เพิ่มการปรับคำลงท้ายและคำอ้างอิงใกล้ตัวสำหรับคำชวน
   - สำนวนใหม่ไม่จำเป็นต้องเหลือแต่คำใน ReadFiller/JoinFiller ถึงจะใช้ action ได้
   - เส้นทาง semantic ต้องมีเจตนาขออ่าน/แบ่งปัน/ชวนเดินจริง ตรวจคำปฏิเสธ คำอ้างอิง สมมติ สถานะ และขอบเขตเป้าหมาย
   - อ่านหลักฐานได้เฉพาะชื่อ/alias ที่ยืนยันหรือ deictic ที่ใช้บริบทได้ ไม่ให้โมเดลเลือกแทนเมื่อมีหลายชิ้น
   - คำขอรหัส/ตีความ/อ่านย้อนยังไม่ถูกยกเว้น hint policy; ไม่มีสิทธิ์อ่าน answer/guidance/forbidden/unrevealed facts
   - ชื่อในคำขอและ operation ต้องตรงกับ proposal ตรวจ availability, range, NPC, room, conversation และ task ปัจจุบันซ้ำ
   - การสร้าง prompt ตรวจ scope แบบ pure ไม่สอน NPC หรือเปลี่ยนสถานะ และไม่ตัด semantic action menu ทิ้งเพราะตีความผิดว่าเป็นคำขอ hint

2. คำชวนที่ยืนยันได้ไม่ขึ้นกับ provider
   - IsJoinRequest ที่ผ่านใช้ Follow ของ controller เดิมทันที ไม่ส่ง API ก่อน
   - รวมคำชวนยาวเดิมที่ก่อนหน้านี้ทำงานออฟไลน์แต่ต้องรอ provider เมื่อเปิด AI
   - null / exception / hanging provider ไม่กระทบเส้นทางนี้ เพราะไม่เริ่ม provider ตั้งแต่แรก
   - คำสั่งหยุดยัง local และการเดินเริ่มหลังปิดบทคุยตามเดิม

## ตัวอย่างที่ตรวจ

- มาช่วยดูอันนี้ด้วยกันหน่อย
- มาดูอันนี้ด้วยกันหน่อยนะจ๊ะ
- ขอให้เธอมาตรวจหลักฐานเป็นเพื่อนหน่อย
- ช่วยอ่านโน้ตให้ฟังหน่อยนะจ๊ะ
- ช่วยอ่านข้อความในโน้ตนี้ให้เราฟังอีกครั้งได้ไหม — ทดสอบว่าผ่าน provider จำลองจริง ไม่ใช่เพิ่มคำทุกคำเข้ารายการ local
- ช่วยอ่านข้อความที่อยู่บนโน้ตนี้ให้เราฟังอีกครั้งได้ไหม
- ช่วยมาดูด้วยกันสักครู่ได้ไหม — ทดสอบ semantic proposal

เส้นทาง semantic ยังมีขอบเขต ไม่ใช่คำรับประกันว่ารองรับภาษาอิสระทุกแบบ
ไม่มีการเพิ่ม TTS, การเดินไปยังวัตถุใดก็ได้ หรือการแยกหลายคำสั่ง/หลาย comma
สำนวนที่ต้องใช้ model ยังขึ้นกับคำตอบ provider; คำขอที่เกมยืนยันเองได้ไม่ขึ้นกับ provider แล้ว

## หลักฐาน

ก่อนแก้: Unity PlayMode Before.xml ล้มเหลว 3/3 ตรงกับสองประเด็นที่ตรวจพบและ semantic invitation นอก local filler
หลังแก้: Expanded.xml ผ่าน 57/57 รวม search scope และ social dialogue ก่อนเพิ่ม relative-clause variant ใน test เดิม
เพิ่ม 7 PlayMode regression และปรับ regression คำชวนเดิมให้ยืนยันว่าไม่ต้องเรียก provider
ไฟล์ผลตรวจอยู่ใต้ Logs/NaturalCompanionFix_20261016/ เป็น simulated-provider verification ไม่ใช่ real KKU evaluation

## ผลตรวจรุ่นสุดท้าย

Unity 2021.3.16f1:

| ชุดตรวจ | ผ่าน | ล้มเหลว | ข้าม |
| --- | ---: | ---: | ---: |
| EditModeFinal | 1281/1281 | 0 | 0 |
| PlayModeFinal | 301/301 | 0 | 0 |

รวม 1582 กรณี เพิ่ม 7 PlayMode regression จากฐาน af580ce
PlayModeFinal รวมกรณีอ่านร่วมกับเรา/ปฏิเสธการเลือกแทนคำขอสองเป้าหมาย และ relative-clause wording แล้ว
EditModeFinal.xml SHA256: F76DB91DB85A9BE312B24FFBCEB88FBCC46DCAA5CE314D78A1A37112CC5270BD
PlayModeFinal.xml SHA256: 481EFB2306831D3E6CA93555353BBCE7EFCB497B29B9535A365F511263AE8FC4
ผลทดสอบ/เดโมใต้ Logs เป็น local artifacts ไม่เข้า Git; ไม่มี real KKU evaluation หรือ manual Windows visual QA

## เดโมรุ่นแก้ไข

- Build.log: AI-enabled demo build: Succeeded, errors=0
- Output: Logs/CompanionAiBuilds/20261016-131003-bd2af5/
- ZIP: Logs/CompanionAiBuilds/MysteryGame-AI-Enabled-NaturalCompanion-Fix-20261016-131003.zip
- ZIP: 74,189,601 bytes, 179 entries; SHA256 C754F3A2685CF96C8894D2A7BC93BE19A183C76B5E2F2A522F4E5E2A3BD001F3
- ตรวจ DLL ด้วย Cecil: มี semantic validation รุ่นแก้ไขและ ambiguity guard ล่าสุด พร้อม normal bootstrap ไม่บังคับ offline
- ตรวจ runtime/readmes ครบและอ่านทุก entry ได้ ไม่รวม UserSettings/api_keys.json/*api_key.txt/BurstDebugInformation_DoNotShip
- มี README_AI_TH.md และ NATURAL_COMPANION_FIX_TH.md ภายใน ZIP; แตกทั้ง ZIP ก่อนเปิด MysteryGame.exe
- เก็บ ZIP รุ่นก่อนหน้าไว้ ไม่ทับและไม่เปลี่ยน save/key ของผู้เล่น
