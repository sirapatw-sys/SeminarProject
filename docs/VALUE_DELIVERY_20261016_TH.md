# ส่งมอบการทำตามแผนคุณค่า NPC — 16 ตุลาคม 2026

ฐานก่อนทำ: 27c1626 / branch codex/npc-cothinking
Fetch ก่อนเริ่ม: ahead 9 / behind 0 เทียบ origin/develop; ไม่รวม/แก้ develop ในรอบนี้
ใช้ worktree npc-framework ต่อ รักษาไฟล์ของ checkout หลัก

## พฤติกรรมใหม่
- AI action ต้องมีเจตนาขอให้ลงมือในข้อความจริง ตรวจชนิด action หลังแก้เป้าหมายด้วย
- Regression ครอบคลุมชื่อสิ่งของ 8 แบบ × proposals 2 แบบ และคำขอจริง 4 แบบ × 2 proposals รวมทั้งการตอบชื่อหลังถามกลับในชุดเดิม
- Alice ส่ง ! ชวนทบทวนเมื่อมี observed.drawer_failed จาก interaction จริง และลิ้นชักยังไม่เปิดใน Room01
- บทชวนใช้ข้อมูล MiniEventData/DialogueData และ event controller เดิม ไม่มี action choices, AI generation หรือรางวัลจากบทเปิด พิมพ์ตอบเองได้
- ไม่บังคับเปิดคุย ไม่ปิด keypad; อ่านแล้วเก็บ completed receipt, ละเลยไม่เสียคะแนน, หมดอายุเมื่อสำเร็จ/เปลี่ยนห้อง/reset; หากละเลยจะพักตาม cooldown 180 วินาที ไม่ขึ้นซ้ำทุกครั้งที่ลอง
- Event อยู่ท้ายรายการเดิม จึงรักษาลำดับความสำคัญ story beats ที่มีอยู่ก่อน
- จังหวะนี้เป็นต้นแบบสำหรับวัดผล ยังไม่ได้พิสูจน์ด้วยคนว่ารู้สึกเป็นเพื่อนหรือช่วยให้เล่นดีขึ้น

## สถานะตามแผน
| ขั้น | ทำแล้ว | ยังรอ |
| --- | --- | --- |
| A สิทธิ์ลงมือ | แก้ runtime และผ่าน regression | คุณภาพ provider จริง |
| B เดโมความร่วมมือ | เพิ่ม soft invitation จาก witnessed failure; suite/build ผ่าน; มี runbook | เล่น Windows จริง ภาพ/เสียง/การอ่านท่าทาง และเส้นทางเดี่ยว |
| C ผู้เล่นใหม่ | protocol/แบบเก็บผลเดิมพร้อม | ผู้เล่นจริง 3–5 คน |
| D ใช้ต่อ | integration guide, ภารกิจ/แบบบันทึก, internal trial ZIP, isolated-project tests | ผู้พัฒนาอื่นติดตั้งและสร้างธีมใหม่ด้วยตนเอง, สิทธิ์เผยแพร่จากเจ้าของทีม |
| E นำเสนอ | เดโมใหม่ หน้าแนะนำโครงการ บทพูด 5 นาที และ checklist คลิป | คลิป/ภาพจากการเล่นจริง ผลคนเล่นและผู้พัฒนา |

ไม่มีการใช้ real AI key/quota, push, PR, merge หรือส่งข้อความให้เพื่อน

## การตรวจที่รันจริง
| ชุด | ผ่าน | ล้มเหลว | ข้าม |
| --- | ---: | ---: | ---: |
| ก่อนแก้ consent: regression ที่ขยาย | 0/1 | 1 | 0 |
| หลังแก้ consent: focused PlayMode | 52/52 | 0 | 0 |
| soft invitation ผ่าน keypad จริง | 2/2 | 0 | 0 |
| EditMode ทั้งเกม (สุดท้าย) | 1281/1281 | 0 | 0 |
| PlayMode ทั้งเกม (สุดท้าย) | 277/277 | 0 | 0 |
| Package EditMode ในโปรเจกต์แยก | 22/22 | 0 | 0 |
| Repair Workshop PlayMode ในโปรเจกต์แยก | 1/1 | 0 | 0 |

Suite รวมเกม 1558 และ isolated package 23 เป็นคนละชุด ไม่ใช่จำนวนผู้เล่นหรือคะแนนคุณภาพประสบการณ์
ระหว่างเพิ่ม event พบ content test เดิมบังคับทุก event มี 1–4 choices; ปรับให้ 0–4 เมื่อ completeOnChoice=false ตาม runtime contract เดิม และยังบังคับ 1–4 เมื่อ completeOnChoice=true
ผลสุดท้ายอยู่ใน Logs/ValueDelivery_20261016/; เก็บผลที่ไม่ผ่านระหว่างพัฒนาไว้แยก ไม่ใช้แทนผลสุดท้าย

SHA256:
- VerifiedEditMode.xml: 26618512587D7E505308CAF3325AA24A53E2DAD9BF2570366423F5507DE879B3
- VerifiedPlayMode.xml: 8114B153A1F3E10D434E08ADB34C83686E8B7D6BBE2F2CD6E99112543C731317
- PackageEditMode.xml: 0F226A55C76FF25384589D4140FA656ABA320258C118CEA4DF458EE4A3C78FCB
- PackagePlayMode.xml: 80F3EB051494253F68CCE1C33A08AE50F1CD73200558A5DA1DC855F829125DCE

## เดโมและชุดทดลอง
เดโมสุดท้าย:
- Logs/CompanionPlaytestBuilds/20261016-072931-0ba7ce/
- ZIP: Logs/CompanionPlaytestBuilds/MysteryGame-Offline-20261016-072931.zip
- 81,903,836 bytes; ตรวจ runtime/data ครบ 211 entries ไม่รวม Burst debug information
- SHA256: 9022C77C68DF694FDDB01F7CB4A0B797FA4424D32282E7B09AFFD49383C6A846
- BuildFinal.log ยืนยัน Succeeded, errors=0; สร้างออฟไลน์ ปิด AI dialogue/STT/model-list requests
- แตก ZIP ทั้งโฟลเดอร์แล้วเปิด MysteryGame.exe เลือก New Game ใช้ runbook ทดลอง feature
- Trace ใน Application.persistentDataPath/Playtests มี IDs/เวลา ไม่เก็บข้อความ; แจ้งผู้เล่นตาม protocol ก่อนทดลอง

ชุดโมดูลสำหรับทดลองภายใน:
- Logs/ValueDelivery_20261016/CompanionReasoning-0.2-InternalTrial-20261016.zip
- 36 entries มี package.json, core, sample, tests, README/INTEGRATION และแบบทดลองผู้พัฒนา
- SHA256: B776F4447565055F2E5C1BE5702306145F928FDD65741AE2069C5AFB0EA5F8D8
- แตก ZIP แล้ว Package Manager > Add package from disk เลือก package.json ใน com.mysterygame.companion-reasoning
- Import Repair Workshop จาก Samples; ทำตาม README/INTEGRATION และแบบทดลอง
- เป็นชุดใช้ทดลองภายใน สิทธิ์เผยแพร่สาธารณะยังรอทีมกำหนด

Logs/ZIP เป็น local artifacts ที่ไม่เข้า Git; คู่มือ โค้ด ข้อมูล และ tests เข้า branch เพื่อทำซ้ำได้
Build success เป็นหลักฐาน compile/package ไม่แทนการเปิดเล่นด้วยคนหรือ visual/audio QA
Trace invitation เดิมนับ discovery HUD draw; ! ของ event ใหม่นี้ให้ผู้ดูแลจดตาม protocol ไม่อ้างว่า trace วัดการมองเห็นของผู้เล่น

## เอกสารเริ่มใช้งาน
- [หน้าแนะนำ](NPC_PROJECT_SHOWCASE_TH.md)
- [Runbook ผู้สาธิต](ROOM01_DEMO_RUNBOOK_TH.md)
- [บทพูด 5 นาที](NPC_PRESENTATION_SCRIPT_TH.md) — เป็นบทพูดและแผนภาพประกอบ ยังไม่ใช่ไฟล์สไลด์/คลิป
- [Protocol ผู้เล่นใหม่](ROOM01_PLAYTEST_PROTOCOL_TH.md) และ [แบบผลผู้เล่น](PLAYTEST_RESULTS_TH.md)
- [ภารกิจผู้พัฒนา](COMPANION_DEVELOPER_TRIAL_TH.md) และ [Host integration](../FrameworkPackages/com.mysterygame.companion-reasoning/INTEGRATION.md)

ขั้นถัดไปคือเปิดเดโมกับคนจริง ตรวจ visual/audio ด้วย runbook แล้วทดลองผู้เล่นโดยไม่แจกคำสั่ง ให้เพื่อนใช้ trial ZIP ตามคู่มือและบันทึกเวลาจริง เลือกแก้จากสามอุปสรรคหลักที่พบ แทนการเพิ่มระบบใหญ่ก่อนมี consumer ที่สอง
