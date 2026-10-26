# Companion Reasoning: NPC คู่ร่วมแก้ปัญหา
สถานะ: ต้นแบบ Unity + โมดูลร่วมคิดจากหลักฐาน 0.2.0

ผู้เล่นกับ NPC พบข้อมูลคนละส่วน แลกหลักฐาน ขอความช่วยเหลือ และเสนอวิธีทดลองร่วมกัน
NPC ตอบตามสิ่งที่รู้และระดับความช่วยเหลือที่เกมอนุญาต ส่วนผู้เล่นเป็นผู้เลือกและลงมือแก้ปริศนา

## เหมาะกับใคร
ทีมอินดี้และนักศึกษาที่ทำเกมปริศนา/สืบสวนใน Unity และต้องการควบคุมว่า NPC รู้ ช่วย และเปิดเผยอะไรได้
เริ่มทดลองด้วย Repair Workshop โดยไม่ต้องมี API key; ทดสอบเกม Room01 ด้วย offline Windows demo

## สามสิ่งที่ควรเห็นในเดโม
- ขอช่วยแล้ว NPC เดินไปจับกลไกจริง งานสำเร็จเมื่อผู้เล่นทำอีกด้าน
- ก่อนมีหลักฐาน NPC ยังประเมินไม่ได้ หลังแลกข้อมูลจึงร่วมพิจารณาสมมติฐาน
- ระดับความสัมพันธ์กำกับความช่วยเหลือ โดยการคุยไม่เฉลยหรือแก้ปริศนาแทนผู้เล่นโดยอัตโนมัติ

## เริ่มลอง
- ผู้เล่น: อ่าน [protocol](ROOM01_PLAYTEST_PROTOCOL_TH.md) และใช้ build ที่ระบุใน [progress](SOL_NPC_VALUE_PROGRESS_TH.md)
- ผู้เล่นที่ต้องการคุยกับ AI จริง: [คู่มือตั้งค่ารุ่น AI-Enabled](AI_ENABLED_DEMO_TH.md) โดยใช้คีย์ของตนเอง
- ผู้สาธิต: [เส้นทางเดโมและ checklist](ROOM01_DEMO_RUNBOOK_TH.md)
- ผู้นำเสนอ: [บทพูด 5 นาทีและภาพที่ต้องเตรียม](NPC_PRESENTATION_SCRIPT_TH.md)
- ผู้พัฒนา: [ติดตั้ง package](../FrameworkPackages/com.mysterygame.companion-reasoning/README.md) → [ต่อ host](../FrameworkPackages/com.mysterygame.companion-reasoning/INTEGRATION.md) → [ภารกิจทดลองใช้](COMPANION_DEVELOPER_TRIAL_TH.md)
- ทีมโครงการ: [แผนและเกณฑ์คุณค่า](NPC_VALUE_AND_ADOPTION_PLAN_TH.md)

## หลักฐานและข้อจำกัด
ผล automated tests/build อยู่ใน progress พร้อมข้อจำกัด mock/offline
ยังไม่มีผลผู้เล่นจริงหรือผู้พัฒนาอื่นยืนยันความสนุก ความรู้สึกมีเพื่อน หรือเวลาติดตั้ง
โมดูลแยกครอบคลุม reasoning/query/effect registry; movement/UI/save/provider เป็นระบบของเกม host
ไม่อ้างว่าเหนือเกมอื่นโดยรวม และยังไม่เปิดเผยแพร่ package พร้อม license สาธารณะ
หน้าข้อมูลนี้เป็นเอกสารใน repository ยังไม่มีเว็บไซต์หรือลิงก์ดาวน์โหลดสาธารณะที่เผยแพร่แล้ว
