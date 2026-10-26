# ตรวจและปรับประสิทธิภาพ NPC / UI

ฐานงาน `36dbc36` บน `codex/npc-cothinking` ส่งมอบวันที่ 17 ตุลาคม 2026

## สิ่งที่เปลี่ยน

- `CompanionDiscovery`: ตรวจ fact ที่ค้นพบจริง สิทธิ์ และสถานะรายงานโดยตรง ไม่สร้าง knowledge context/รายการคำสั่ง AI สำหรับการแสดงเครื่องหมายเหนือหัว เพิ่ม `HasPending` ที่หยุดเมื่อพบรายการแรกและไม่สร้างรายการผลลัพธ์ทั้งหมด
- `RoomWorldRegistry`: ลงทะเบียนผ่าน Configure/OnEnable/OnDisable/OnDestroy ค้นเฉพาะวัตถุที่ลงทะเบียนแทนการสแกนทั้งฉากทุกคำขอ ตรวจ visibility และ ID ซ้ำสดทุกครั้ง เริ่ม Play มี bootstrap scan หนึ่งครั้ง รองรับการตั้งค่าไม่ reload domain/scene
- `NpcCompanionController.Find`: ใช้ทะเบียน instance และกรองห้อง/สถานะ active ขณะค้น คงลำดับ instance ID และพฤติกรรม lookup ของ component ที่ disabled บน GameObject ที่ยัง active; การลงมือยังผ่าน guard เดิม
- `CompanionChatPanel`: รวมข้อความในเฟรมเดียวกัน จัด layout เฉพาะประวัติแชทใน LateUpdate ไม่เรียก ForceUpdateCanvases ทั้งเกมทุกข้อความ และไม่จัด layout แชทที่ปิดอยู่ ผลงานยังเก็บไว้แสดงเมื่อเปิดกลับมา

ไม่ cache คำตอบเรื่องสิทธิ์หรือความรู้ของ NPC เพื่อให้การเปลี่ยน flag, forbidden facts, save/load, visibility และการทำลายวัตถุมีผลทันที ระบบเดิน/ตรวจสิ่งกีดขวางและกฎคำใบ้ใช้เส้นทางเดิม

## ผลวัดก่อนและหลัง

Unity 2021.3.16f1 Editor แบบ batchmode/nographics, Room01, provider จำลอง ไม่เรียก API จริง Warm-up 10 ครั้ง แล้ววัด 3 samples ต่อกรณี ตารางใช้ median ของเวลารวมตามจำนวนครั้งเดียวกัน

| กรณี | จำนวนครั้งต่อ sample | ก่อน (ms) | หลัง (ms) | เวลาลดลง |
| --- | ---: | ---: | ---: | ---: |
| ตรวจรายการหลักฐานค้างรายงาน | 100 | 14.3824 | 0.5208 | 96.38% |
| ตรวจเครื่องหมายแจ้งหลักฐาน | 100 | 14.2597 | 0.3965 | 97.22% |
| หา NPC ในห้อง | 500 | 3.3061 | 0.2707 | 91.81% |
| หาวัตถุตาม ID | 500 | 13.1797 | 0.4752 | 96.39% |
| รับข้อความแชทต่อเนื่องในเฟรมเดียว | 100 | 0.9519 | 0.0044 | 99.54% |

ข้อมูล: `Logs/NpcOptimization_20261016/BeforeValidated.xml` เทียบ `AfterFocused.xml` ผ่าน `MeasureCompanionHotPathsWithoutUsingAProvider` ใน PlayMode tests

ตัวเลขเป็นเวลาเฉพาะฟังก์ชันใน benchmark ไม่ใช่เปอร์เซ็นต์ความเร็วทั้งเกมหรือ FPS ใน Windows player กรณีข้อความแชทวัดเฉพาะการรับข้อความเป็นชุด งาน layout หลัง optimize ย้ายไปทำครั้งเดียวใน LateUpdate และไม่ได้รวมในเวลารับข้อความนี้

ไม่รายงานจำนวน bytes/GC reduction: runtime นี้มี GetAllocatedBytesForCurrentThread แต่ตัวนับคืนศูนย์แม้ทดลองจอง array 4096 bytes จึงถือว่าวัดไม่ได้และบันทึก bytes=-1; `Before.xml` ฉบับแรกที่ยังไม่ตรวจ counter ไม่ใช้เป็นหลักฐาน allocation

## การตรวจความถูกต้อง

เพิ่มกรณีทดสอบการเปิด/ปิดและทำลายวัตถุ, ID ซ้ำ/เปลี่ยน ID, การแก้รายการผลค้นหาไม่กระทบ registry, disable/enable/destroy NPC และเทียบผลกับ scene lookup เดิม, เปลี่ยนสิทธิ์หลักฐานสดและ restore snapshot, ข้อความล่าสุด/ผลที่เกิดระหว่างปิดแชทและการเลื่อนประวัติเมื่อเปิดใหม่

ชุดเฉพาะ World + Discovery ผ่าน 40/40 ใน `AfterFocused.xml` ผลตรวจทั้งระบบและ build ฉบับสุดท้ายบันทึกเพิ่มหลังจบในหัวข้อถัดไป

ผลทั้งระบบ: `PlayModeFinal.xml` ผ่าน 331/331 และ `EditModeFinal.xml` ผ่าน 1,281/1,281 รวม **1,612/1,612**; failed=0, skipped=0 เพิ่ม regression lifecycle/UI 4 กรณีและ benchmark 1 กรณีจากฐานงานเดิม ใช้ AI จำลอง ไม่ใช้โควตา provider จริง

Windows AI-enabled release build สำเร็จ errors=0: `Logs/CompanionAiBuilds/20261016-171028-3fba1c/` ดู `Logs/NpcOptimization_20261016/Build.log` แพ็กเกจ `MysteryGame-AI-Enabled-Optimized-20261017.zip` รวมคู่มือและรายงานนี้ ไม่มี UserSettings หรือ Burst debug files; build/tests ไม่ใช่ manual visual QA หรือผล FPS ของ Windows player
