# ส่งมอบเดโมเปิด AI — 16 ตุลาคม 2026
ฐานก่อนทำ: 0946e9a บน codex/npc-cothinking; fetch แล้วก่อนเริ่ม ahead 10 / behind 0 เทียบ origin/develop
รอบนี้ไม่ push/PR/merge และไม่ใช้ key/quota จริงของผู้เล่น

## ใช้งาน
ZIP local: Logs/CompanionAiBuilds/MysteryGame-AI-Enabled-20261016-113904.zip
แตกทั้ง ZIP ก่อนเปิด MysteryGame.exe แล้วกดตั้งค่าหรือ F10 เลือก KKU IntelSphere ใส่คีย์และโมเดลที่บัญชีใช้ได้ กดบันทึกและทดสอบการเชื่อมต่อ
ดู [คู่มือภาษาไทย](AI_ENABLED_DEMO_TH.md); มี README_AI.txt และ README_AI_TH.md ใน ZIP ด้วย
เดโม offline รุ่นเดิมยังอยู่ ใช้กับการทดสอบ flow ที่ไม่ต้องการใช้โควตา

## สิ่งที่เปลี่ยน
- เพิ่ม BuildAiEnabledDemo ใน CompanionPlaytestTools ใช้ Windows release / BuildOptions.None และไม่เพิ่ม NPC_PLAYTEST_BUILD
- แยกโฟลเดอร์ AI build จาก offline build สร้างชื่อใหม่ทุกครั้ง ไม่ทับไฟล์เดิม
- หาก Standalone project defines ยังมี NPC_PLAYTEST_BUILD จะหยุดพร้อมเหตุผล เพื่อกันส่งรุ่น offline ผิดชื่อ
- หน้าตั้งค่าแสดงโหมด offline ให้ตรง; บันทึกโมเดลได้แต่ไม่อ้างว่าขาด key ทั้งที่ติดข้อจำกัดรุ่น
- ปุ่มทดสอบ/โหลดรายชื่อโมเดลใน offline ไม่เรียก provider แม้ override พร้อมใช้งาน
- การบันทึกรุ่นปกติยืนยันเฉพาะการตั้งค่า ไม่อ้างว่าคีย์ผ่านการเชื่อมต่อก่อนกดทดสอบ
- คีย์ที่พิมพ์ยังใช้เฉพาะ session; model/provider/endpoint เก็บด้วย PlayerPrefs ตามเดิม
- ระบบ hint/fact/action authorization ไม่ได้เปลี่ยน

## หลักฐาน
| ชุดตรวจ | ผ่าน | ล้มเหลว | ข้าม |
| --- | ---: | ---: | ---: |
| AiRequestLifecycle focused | 36/36 | 0 | 0 |
| EditMode ทั้งเกม | 1281/1281 | 0 | 0 |
| PlayMode ทั้งเกม | 279/279 | 0 | 0 |

เพิ่มสอง regression สำหรับการบันทึกก่อนทดสอบผ่าน mock และข้อความ/การหยุด request ของ offline
Saving ไม่ส่ง request; connection test จำลองส่งหนึ่งครั้งและรับคำตอบสำเร็จ
ผลอยู่ใน Logs/AiEnabledDelivery_20261016/; เป็น simulated/offline verification ไม่ใช่ real-provider evaluation

SHA256:
- EditMode.xml: 16B183F9C0F97577CEC201231889504372B5228FD8099D619A9EAD44424E0806
- PlayMode.xml: 27953C03CE4EBC963A4D7A6216C42C53A9D8941FDBF3834B0AF110E4D488B615

## Build และ ZIP
- Build.log ยืนยัน AI-enabled demo build: Succeeded, errors=0
- Output: Logs/CompanionAiBuilds/20261016-113904-75416d/
- ตรวจ compiled MysteryGame.Runtime.dll ด้วย Cecil: Bootstrap เริ่มจาก Debug.get_isDebugBuild และ branch ออกได้ เป็น normal bootstrap ไม่มี forced-offline initialization แบบ NPC_PLAYTEST_BUILD
- Release build ไม่เริ่ม trace อัตโนมัติ และไม่ใช้ offline provider override ตามค่าเริ่มต้น
- ZIP 74,180,527 bytes, 180 entries; ตรวจ exe/UnityPlayer/data/readmes ครบ และไม่มี UserSettings/api_keys.json/*_api_key.txt
- ไม่รวม MysteryGame_BurstDebugInformation_DoNotShip
- ZIP SHA256: 113F0212B5B068B28DFB51932634DC07D92D785A81D45457EAFA73024B4F427D

Build/ZIP เป็น local artifacts ไม่เข้า Git; tools, tests และคู่มือเข้า branch
ยังไม่ได้เปิดเล่น Windows GUI หรือยิงผู้ให้บริการจริง ผู้เล่นเป็นผู้ใส่คีย์และกดทดสอบเอง ซึ่งจะใช้โควตาบัญชี
