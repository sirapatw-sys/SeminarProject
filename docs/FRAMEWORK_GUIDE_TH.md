# คู่มือ framework — อัปเดต 5 ตุลาคม 2026

ปรับต้นแบบให้เพิ่มเนื้อหาผ่านข้อมูลได้มากขึ้น โดยคงเกม Room01 → Room02 → Room03 ไว้ ไม่ได้เปลี่ยนให้เป็นเครื่องมือสร้างเกมทุกประเภทสำเร็จรูป

## เก็บปัญหาจากการตรวจซ้ำ 5 ตุลาคม

เพิ่มระบบเลือกหลักฐานไปให้ NPC ดู และเรื่องต่อเนื่องของ Alice ใน Room01 แล้ว ดู [วิธีเล่นและวิธีตั้งข้อมูล](EVIDENCE_AND_EVENTS_TH.md) ฟีเจอร์ใหม่เป็นทางเลือก ไม่เปลี่ยนเงื่อนไขผ่านห้องเดิม

- ข้อมูลห้อง/NPC ต้องมาจาก GameDefinition ที่เลือกอยู่ ไม่หยิบ Resources ของเกมเดิมมาแทนเมื่อ ID ซ้ำ เปลี่ยน GameDefinition แล้ว cache จะเปลี่ยนตามโดยอัตโนมัติ หากตั้ง GameDefinition อยู่แต่ไม่มี ID นั้น จะได้ null แทนข้อมูลของเกมอื่น
- บทลาที่รอหลังตอบปริศนาผูกกับรอบบทสนทนาเดิม ปิดแชท เปลี่ยน NPC หรือเปิดแชทรอบใหม่แล้วจะไม่เด้งแทรก แต่บทลายังเล่นตามปกติถ้าผู้เล่นคุยรอบเดิมอยู่
- NpcPuzzleData.hideOnSolved=false ให้ NPC อยู่และคุยต่อได้ ใช้ postSolvedDialogue เป็นบทหลังจบ หรือใช้ solvedDialogue เมื่อไม่ได้กำหนดบทแยก ไม่รับของถวายซ้ำและไม่ให้รางวัลคำตอบปริศนาซ้ำ
- คำตอบที่อ้างข้อมูลด่านไม่ใช้ข้อความที่โมเดลแต่งหรือ paraphrase เอง ให้โมเดลเลือก `{fact:factId}` ของ fact ที่ NPC มีสิทธิ์รู้ แล้วระบบแทนด้วย statement ที่ผู้สร้างเขียนไว้ ตรวจ referencedFactIds ว่าถูกใช้จริง ไม่รับ ID ที่แนบมาเพื่อรับรองข้อความแต่ง
- กันคำเฉลยที่แทรกเครื่องหมาย/ตัวอักษรล่องหน/เลขไทย/เลขเต็มความกว้าง รวมถึงเลขที่สะกดเป็นคำตามชุดทดสอบ บทคุยอิสระที่มีคำแนะนำ/ศัพท์กลไกเกมนอก fact จะใช้บทสำรองพร้อมแจ้งผู้เล่น
- Mini event ตรวจสิทธิ์ facts ซ้ำตอนผู้เล่นกดคุย ไม่ใช่แค่ตอน AI สร้างบท หากสถานะเปลี่ยนจนข้อมูลนั้นใช้ไม่ได้จะเลือกบทสำรอง ตัวเลือกยังใช้ actions ที่ผู้สร้างกำหนด
- NpcEventController มีเวลารอ AI สูงสุดของตัวเอง (25 วินาทีเริ่มต้น ใช้เวลาจริงแม้เกมหยุดเวลา) คำขอที่จบโดยไม่ตอบหรือเกิดข้อผิดพลาดใช้บทสำรอง เหตุการณ์เนื้อเรื่องแทรกแทนคำขอคุยทั่วไปที่ยังรอได้ คำตอบเก่า/ซ้ำและคำตอบหลังปิด NPC ไม่ทับเหตุการณ์ใหม่
- PersonalFact ตรวจ statement และ protectedTerms ทั้งข้อความ รวมกรณีแยกคำข้ามบรรทัด ความลับที่ยังล็อกไม่ถูกส่งในข้อมูลตัวละครให้ AI เรื่องส่วนตัวที่เปิดแล้วต้องอ้าง fact canon ไม่เขียนรายละเอียดเองนอก fact
- ข้อมูลห้องที่ห้ามเปิดเผยตรวจ protectedTerms ทั้งแต่ละบรรทัดและข้อความรวม รวมบท mini event กับตัวเลือก การสะกดตัวเลขไทย/อังกฤษแยกบรรทัดหรือแทรกเครื่องหมายระหว่างตัวอักษรไม่ใช้หลบการตรวจได้ตามเคสทดสอบ
- ค่าความต้องการและอารมณ์ NPC ยังเพิ่มตามเวลาและเรียกเหตุการณ์ตาม threshold ได้ แต่ไม่เพิ่ม worldRevision ที่ใช้เลือกบทกลับมาคุย การปล่อยเวลาเฉย ๆ จึงไม่ทำให้ Alice อ้างว่าผู้เล่นทำด่านคืบหน้า ส่วน flag และ inventory ที่เปลี่ยนจริงยังเปลี่ยนบททักทายได้
- บททักทาย Alice ให้ความสำคัญกับความเปลี่ยนแปลงและอารมณ์ปัจจุบันก่อนความทรงจำเรื่องสัญญา บทสัญญาจำกัดเฉพาะ Room01 ด้วย ConditionType.CurrentScene และหมุนบทเมื่อคุยซ้ำ โดยไม่ลบผลสัญญา
- ปฏิกิริยาต่อโน้ตเลือกตาม drawer_opened/door_unlocked ก่อนคำแนะนำทั่วไป จึงไม่ชวนทำขั้นที่สำเร็จแล้ว และรางวัลหลักฐานยังได้เพียงครั้งเดียว

### รูปแบบข้อมูลที่ AI ใช้

บทคุยทางสังคมและความรู้สึกยังสร้างใหม่ได้ตามบุคลิก เช่น reply="คิดถึงบ้านจัง ดีใจที่เธอรับฟัง" และ referencedFactIds=[]

ถ้าพูดข้อเท็จจริง ให้แต่ละ fact อยู่บรรทัดแยก เช่น reply="{fact:door_locked}" และ referencedFactIds=["door_locked"] ระบบจะแสดง statement จริง ไม่แสดงวงเล็บ และไม่ให้โมเดลเติมข้อความขยาย/ปฏิเสธข้อเท็จจริงในบรรทัดนั้น ถ้าไม่มี fact ที่ยืนยันได้ ให้ตอบว่าไม่รู้ คำใบ้ยังผ่าน HintReply ตามขั้นและความสัมพันธ์เหมือนเดิม

RoomKnowledgeData.gameplayTerms เพิ่มศัพท์กลไกเฉพาะห้องผ่าน Inspector ได้ เช่นชื่อเครื่องส่งสัญญาณ เพื่อไม่ต้องแก้โค้ดสำหรับศัพท์ของด่านใหม่ บทสำรองที่ผู้สร้างเขียนไว้เป็นเนื้อหาที่เชื่อถือได้ จึงไม่ถูกบังคับผ่านรูปแบบข้อความของโมเดล

ข้อจำกัด: ช่องบทคุยอิสระยังใช้การกรองศัพท์/รูปแบบข้อความ ไม่ใช่ semantic verifier ที่พิสูจน์ความหมายได้ทุกภาษา จึงไม่อ้างว่าป้องกัน hallucination ทุกแบบได้ 100% ต้องเติม gameplayTerms ของเนื้อหาใหม่และทดสอบกับโมเดลจริงต่อไป ข้อมูลที่แสดงผ่าน fact และเส้นทางคำใบ้ควบคุมจากเนื้อหาที่ผู้สร้างกำหนดได้แน่นกว่า

## สิ่งที่เปลี่ยน

- คำใบ้ใช้ข้อความที่ผู้สร้างด่านเขียนใน PuzzleStep เท่านั้น เลือกขั้นปัจจุบันและระดับตามความสัมพันธ์ ไม่ให้ AI แต่งวิธีผ่านเอง และจดลงสมุดอัตโนมัติ การถามคำใบ้ไม่เพิ่มคะแนนความสัมพันธ์
- บทสนทนาปกติและ mini event ตรวจ referencedFactIds และคำเฉลยที่ระบุใน protectedTerms ก่อนแสดงผล ข้อมูล isPuzzleAnswer ไม่ถูกส่งเป็นข้อเท็จจริงให้โมเดล ข้อความอธิบายขั้นถัดไปที่มีเฉลยก็ไม่ถูกส่ง
- ลิ้นชักใช้ InputPuzzleData และ InteractionSystem ตรวจทั้งเงื่อนไข คำตอบ และการทำซ้ำ จึงไม่เปิดได้ด้วยการข้ามโน้ตเหมือนทางพิเศษเดิม
- เซนะใช้ NpcPuzzleInteraction + NpcPuzzleData + InputPuzzleData ไม่มีกฎเซนะอยู่ใน DialogueManager/ObjectInteraction อีกแล้ว คำตอบต้องตรงกับรายการหลังตัดช่องว่าง ไม่รับข้อความอย่าง “ไม่ใช่พรุ่งนี้” หรือรายการเดาหลายคำ
- GameDefinition รวมชื่อเกม ฉากแรก เป้าหมายเริ่มต้น บทนำ รายชื่อห้อง/NPC/ไอเท็ม และค่าความสัมพันธ์ที่แบ่งระดับคำใบ้
- ItemData เก็บชื่อ คำอธิบาย และภาพ ไอเท็มโหลดจาก asset reference ที่ไปกับ build ไม่ค้น PNG ในโฟลเดอร์ของเครื่องผู้เล่น ภาพนิ่งคงสัดส่วนเดิม
- GameState ส่ง event ItemAdded โดย GamePresentationBridge เป็นผู้แสดง popup; IAiDialogueProvider เป็นจุดเปลี่ยนผู้ให้บริการ/ตัวจำลอง แทนการผูกหน้าคุยกับผู้ให้บริการตัวเดียว
- ข้อความผู้เล่นเดิมซ้ำในประวัติ NPC คนเดียวไม่รับคะแนนบวกซ้ำ แต่คะแนนลบยังมีผล ประวัตินี้ติดไปกับ save (ตรวจภายในประวัติ 40 turns ล่าสุด ไม่ใช่ระบบป้องกันโกงสมบูรณ์)
- NPC ยังส่ง “!” ชวนคุยเมื่อไม่มี AI หรือคำขอล้มเหลว โดยเลือกจากบทสำรองสองหัวข้อต่อคน ผู้เล่นไม่ตอบก็หมดเวลาและเข้าช่วงพัก ไม่บังคับหยุดเล่น

## ลองใช้งาน

1. เปิด Unity 2021.3.16f1 และฉาก Assets/Scenes/Room01.unity แล้วกด Play
2. คุยกับอลิซ พิมพ์ “ช่วยใบ้หน่อย” แล้วเปิดสมุดด้วย J ข้อความจะตรงกับขั้นที่ยังไม่สำเร็จ
3. แก้ค่าความสัมพันธ์เริ่มต้นใน NPC Profile หรือใช้สถานะทดสอบ เพื่อเปรียบเทียบระดับคำใบ้ต่ำ/กลาง/สูง เซนะยังไม่ให้คำใบ้ตามบุคลิกเดิม
4. ทดลองพิมพ์คำตอบเซนะก่อนให้หนังสือ ประตูต้องไม่เปิด หลังให้หนังสือ พิมพ์คำตอบที่ตรงรายการ เช่น “tomorrow”
5. เมนู Mystery Game > NPC Knowledge Inspector ใช้ดูสถานะจริงใน Play Mode: ห้อง ความสัมพันธ์ ขั้นปริศนา คำใบ้ ข้อเท็จจริงที่พูดได้/ถูกกักไว้ และความลับที่ยังล็อค
6. เมนู Mystery Game > Validate Content ตรวจ ID ซ้ำ ข้อมูลอ้างอิง ปริศนา เงื่อนไข คำเฉลย และฉากใน Build Settings
7. เมนู Mystery Game > Open Two-Room Sample เปิด ObservatoryA แล้วกด Play ตัวอย่างมีโนรา → อ่านบันทึกที่จุดภาพวาด → ใช้รหัสเปิดประตู → ObservatoryB → เปิดเครื่องส่งสัญญาณที่จุดภาพวาด

ด่านตัวอย่างอยู่ที่ Assets/Samples/Observatory ใช้ภาพห้อง/ตัวละครเดิมเป็น placeholder แต่ lore, ID, เงื่อนไข และปริศนาเป็นข้อมูลใหม่ GameDefinitionSelector ในฉากเลือก config ของตัวอย่าง เซฟตัวอย่างใช้ชื่อแยกจาก save.json ของเกมหลัก

## เพิ่มห้อง/NPC โดยไม่แก้ระบบหลัก

- สร้าง RoomKnowledgeData ผ่าน Create > Game > Room Knowledge Data ตั้ง roomId ให้ตรงชื่อ scene และ enteredFlag ให้เป็น ID ใหม่
- เขียน facts, เงื่อนไขเปิดเผย, และขั้นปริศนา แต่ละขั้นต้องมีเงื่อนไขจบและคำใบ้ระดับ Vague; ระดับต่ำจะไม่ยืมคำใบ้ที่ละเอียดกว่า
- เติม gameplayTerms สำหรับคำที่เกี่ยวกับไอเท็ม/สถานที่/กลไกเฉพาะห้อง เพื่อให้ AI ต้องใช้ facts แทนการสร้างข้อความกล่าวอ้างเอง
- สร้าง InteractionData สำหรับวัตถุ ใช้ ConditionRule/ActionCommand ที่มีอยู่ เชื่อม interactionId ของขั้นให้ตรงกัน
- สร้าง InputPuzzleData ถ้าต้องกรอกรหัสหรือคำตอบ คีย์แพดรองรับรหัสเดียว 1–8 หลัก ปริศนาข้อความรองรับหลายคำตอบที่ตรงรายการ
- สร้าง NpcProfileData, DialogueData และ MiniEventData สำหรับตัวละคร แล้วใส่ใน scene และ GameDefinition ของเกมนั้น กำหนด knownFactIds/learnedFacts/forbiddenFactIds ให้ครบ
- หาก NPC ต้องรับไอเท็มก่อนตั้งคำถาม ใช้ NpcPuzzleInteraction และ NpcPuzzleData กำหนดบทแต่ละช่วงและ action ไม่ต้องสร้างสคริปต์ชื่อ NPC ใหม่
- หาก NPC ต้องคุยต่อหลังแก้ปริศนา ให้ปิด hideOnSolved และกำหนด postSolvedDialogue หรือ solvedDialogue ให้มีบทหลังจบ
- เพิ่ม ItemData ลง GameDefinition.items ตั้ง horizontalFrames = 1 สำหรับภาพนิ่ง หรือจำนวนเฟรมสำหรับภาพแนวนอน ใช้ prefab ExampleNpc เป็นตัวอย่างเริ่มต้น แล้วเปลี่ยนภาพ/บทคุย
- เพิ่ม scene ใน Build Settings และรัน Validate Content + Test Runner ทุกครั้ง

เมนู Install Framework Data เป็นตัวติดตั้งครั้งแรกเท่านั้น เมื่อมี GameDefinition แล้วจะไม่เขียนทับเนื้อหาของทีม ไม่ต้องรันซ้ำในโปรเจกต์นี้

## หลักฐานและข้อจำกัด

### การยกเลิกคำขอ AI และผลที่มาช้า (5 ตุลาคม — รอบล่าสุด)

- AiRequestOperation ถือ ownership ของ iterator ทั้งชั้นหลักและชั้นซ้อน ปิดแชท, หมดเวลารอ, disable/destroy หน้าคุย หรือเปลี่ยนห้องแล้วจะยกเลิกและ dispose คำขอ ไม่เพียงทิ้ง callback คืน input และใช้บทสำรองเมื่อ provider คืน null, จบโดยไม่ตอบ หรือเกิด exception
- AiDialogueGenerator.Post dispose UnityWebRequest ที่ยังไม่ได้ส่งต่อให้ผู้เรียก รวมกรณียกเลิกกลางทางและ retry ด้วย backup key ส่วน Generate/GenerateReply dispose response ใน finally ก่อนส่ง callback และ FetchAvailableModels ใช้ using ครอบ native request
- หน้าตั้งค่า AI ผูกผลโหลดโมเดลกับเลขรอบคำขอและ provider/endpoint/key ที่เริ่มขอ ผูกผลทดสอบกับ model เพิ่มด้วย ผลเก่าจะไม่ทับรายการหรือปลดล็อกคำขอใหม่ ปิด/disable หน้าตั้งค่า เปลี่ยน provider บันทึกค่า หรือล้าง key จะยกเลิกคำขอเดิม
- ปุ่มทดสอบการเชื่อมต่อใช้ค่าที่กรอกอยู่บนหน้าจอทันทีผ่าน ApplySettings และใช้ DialogueProviders.Current เหมือนหน้าคุย ไม่ทดสอบด้วยค่าจากการบันทึกครั้งก่อน

ชุดถาวรล่าสุด: EditMode 291/291 (Logs/Codex_RequestLifecycleFix_EditMode_20261005.xml), PlayMode 88/88 (Logs/Codex_RequestLifecycleFix_PlayMode_20261005.xml) รวม 379 เคส เพิ่ม AiRequestLifecycleTests 34 เคส โดยชุดเดิมทั้งหมดผ่าน ชุดเป้าหมาย 34/34 ผ่านแยกด้วย (Logs/Codex_RequestLifecycleFix_Targeted_20261005.xml) ไม่บวกการรันซ้ำเป็นจำนวนเคสใหม่

การตรวจเส้นทางเพิ่มผ่าน 1/1 ครบ 15 ขั้นตอน Room01 → Room02 → Room03 ถึงฉากจบและ journal 13 รายการ (Logs/Codex_RequestLifecycleFix_Journey_20261005.xml) เรียก gameplay handlers กับคำตอบในข้อมูลและปิด random NPC events ไม่ใช่การเดิน/กดปุ่มด้วยคน ไม่รวมใน 379 เคสถาวร ตรวจ GameDefinition ทั้งเกมหลักและตัวอย่างผ่าน และตรวจ MonoBehaviour ในห้าฉากไม่พบ missing scripts/references ตามวิธีตรวจ null reference ที่ยังมี nonzero instance ID (Logs/Codex_RequestLifecycleFix_ScenesAndContent_v3_20261005.log) ไม่ใช่การตรวจ dependency/GUID ทุกชนิด สคริปต์ตรวจชั่วคราวเก็บใต้ Logs/RequestLifecycleFix_20261005 และนำออกจาก Assets แล้ว

Windows build ล่าสุดสำเร็จ 0 errors และ exit code 0 (Logs/Codex_RequestLifecycleFix_Build_20261005.log) ตัวเกมอยู่ที่ Logs/FrameworkSmoke/MysteryGame.exe ต้องใช้คู่กับโฟลเดอร์ข้อมูลข้างกัน ยังไม่ได้เปิดเล่นตรวจภาพ/เสียงด้วยคน

การทดสอบ callback ของ model discovery ใช้ผลจำลอง ไม่โหลดรายการจริงจาก KKU ชุดตรวจ native request ใช้คีย์จำลองและ URL http://127.0.0.1:1 ที่ไม่มีบริการ เพื่อทดสอบการยกเลิก/transport failure และการส่งต่อ ownership ไม่ยืนยัน authentication หรือคำตอบสำเร็จของ API จริง ไม่เขียนเซฟผู้เล่นหรือยืนยันภาพ/เสียงด้วยคน Logs เป็นหลักฐานในเครื่อง ไม่อยู่ใน Git ให้ทีมรัน Test Runner และ build ใหม่หลัง clone รายละเอียดอยู่ที่ Logs/RequestLifecycleFix_20261005/Report_TH.md

### ผลรอบก่อนหน้า: lifecycle เมื่อเริ่มเกมและเปลี่ยนห้อง (5 ตุลาคม)

- GameState.StateReset ส่งสัญญาณเฉพาะเริ่มเกมใหม่ ไม่ส่งเมื่อ restore เซฟ NpcNeedController ใส่ค่าเริ่มต้นตาม scene ให้ทันทีหลัง reset และเติมเฉพาะ metric ที่ขาด จึงไม่ทับค่าที่บันทึกไว้ รวมถึงค่า 0 ที่ถูกต้อง
- InputGate.IsGameplayActive แยกจาก IsBlocked: เมนู, intro และ transition หยุดการเพิ่ม needs/emotions และการเริ่มคำขอ mini event ใหม่ แต่ dialogue/settings ยังเป็นช่วงเล่นที่อาจถือ input อยู่ ไม่หยุด watchdog, การล้าง event หมดอายุ หรือการทิ้ง callback เก่า
- AiSettingsPanel มีเจ้าของสถานะเพียงตัวเดียว component ของ GameManagers ที่ซ้ำไม่แย่ง ownership หรือเคลียร์ modal/prompt ของตัวที่ยังอยู่ ปิด AI settings ก่อนเปลี่ยนห้อง และไม่รับการเปิดระหว่าง transition เมื่อโหลดเสร็จเปิดใหม่ได้ตามเดิม
- การ disable controller/panel ยกเลิก subscription/คืนสถานะ modal; เมื่อ enable อีกครั้งเติมข้อมูลที่ขาดและเปิดหน้าตั้งค่าใหม่ได้

ผลหลังแก้ lifecycle: EditMode 291/291 (Logs/Codex_GameplayLifecycleFix_EditMode_v2_20261005.xml), PlayMode 54/54 (Logs/Codex_GameplayLifecycleFix_PlayMode_v2_20261005.xml) เพิ่ม NpcInitializationTests 8 เคสและ GameplayLifecycleTests 12 เคส รวม 20 regression ถาวร ชุดเดิมทั้งหมดผ่าน รวมกรณีรอหน้าเมนู 40 วินาทีจริง การคงค่าเซฟ และการปล่อยคำขอที่ timeout

การตรวจเส้นทางเพิ่มเติมผ่าน 1/1 ครบ 15 ขั้นตอน Room01 → Room02 → Room03 ถึงฉากจบและ journal 13 รายการ (Logs/Codex_GameplayLifecycleFix_Journey_20261005.xml) เรียก gameplay handlers และใช้คำตอบที่กำหนดในข้อมูล ไม่ใช่การเดิน/กดปุ่มด้วยคน สคริปต์ชั่วคราวเก็บที่ Logs/GameplayLifecycleFix_20261005/GameplayJourneyVerification.cs และนำออกจาก Assets แล้ว ไม่รวมในจำนวน 345 เคสถาวร รายละเอียดหลักฐานและขอบเขตอยู่ที่ Logs/GameplayLifecycleFix_20261005/Report_TH.md

Windows build หลังแก้ lifecycle สำเร็จ 0 errors และ exit code 0 (Logs/Codex_GameplayLifecycleFix_Build_20261005.log); ตรวจข้อมูลด่านผ่าน (Logs/Codex_GameplayLifecycleFix_Content_20261005.log) ตัวเกมอยู่ที่ Logs/FrameworkSmoke/MysteryGame.exe และต้องใช้คู่กับโฟลเดอร์ข้อมูลข้างกัน รอบนี้ใช้ AI จำลองและปิดการเขียนเซฟ ไม่ยืนยัน API จริงหรือภาพ/เสียงด้วยคน Logs ไม่อยู่ใน Git ให้เพื่อนรัน Test Runner, Validate Content และ build ใหม่หลัง clone

### ความปลอดภัยของเซฟ การเปลี่ยนห้อง และคำทักทาย (5 ตุลาคม)

- เมื่อสลับ provider ในหน้าตั้งค่า AI ช่อง key จะถูกล้าง ไม่ยืม key ของ provider เก่าไปส่งให้ endpoint ใหม่ ใส่ key ของเจ้าที่เลือกแล้วกดบันทึก หรือใช้ environment/UserSettings ของ provider นั้นเหมือนเดิม
- SnapshotValidator ตรวจชื่อห้องที่ต้องมี, ID, รายการและช่วงค่าทั้งชุดก่อนแทน GameState พร้อมทำสำเนาข้อมูลซ้อน รองรับเซฟเก่าที่ขาด optional collections ผ่าน TryRestoreSnapshot; snapshot ที่ผิดคืน false และรักษาสถานะเดิม
- SaveSystem.TryLoadSnapshot ใช้เส้นทางเดียวกับการโหลดไฟล์ แต่รับข้อมูลในหน่วยความจำได้ ตรวจว่าห้องอยู่ในเกมที่เลือกและ Build Settings ก่อนเปลี่ยนสถานะ ปุ่มเล่นต่อที่โหลดไม่สำเร็จไม่เริ่มเกมใหม่หรือลบเซฟเอง
- RoomTransitionManager.TryTransitionToRoom คืน false ถ้าปลายทางไม่พร้อม/กำลังเปลี่ยนห้อง ปิด AI settings/dialogue/keypad/item popup/journal และยกเลิก callback เก่าก่อนโหลด สร้าง checkpoint หลังยืนยันว่าโหลดห้องสำเร็จเท่านั้น
- ConditionType.RoomProgressSinceLastTalk (ค่า 10) เปรียบเทียบ completed puzzle steps, revealed room facts, scene และ inventory กับครั้งที่คุยล่าสุด Alice ใช้เงื่อนไขนี้สำหรับบทที่กล่าวถึงความคืบหน้าของห้อง คะแนนสัมพันธ์ ความทรงจำ needs/emotions และ dialogue flags ไม่ทำให้บทนี้ทำงานเอง
- ConditionType.WorldChangedSinceLastTalk (8) และ CurrentScene (9) ยังมีค่าเดิม ไม่เปลี่ยนความหมายของ enum เก่าหรือรูปแบบ JSON save

ผลทดสอบหลังแก้ 5 จุด: EditMode 283/283 (Logs/Codex_RuntimeSafetyFix_EditMode_v2_20261005.xml), PlayMode 42/42 (Logs/Codex_RuntimeSafetyFix_PlayMode_v3_20261005.xml) เพิ่ม RuntimeSafetyTests 31 เคสและ RuntimeSafetyPlayModeTests 8 เคส รวม 39 regression ถาวร ชุดเดิมทั้งหมดผ่าน ใช้ AI จำลอง/คีย์จำลองและไม่เขียนเซฟผู้เล่น หลักฐาน build และข้อจำกัดเพิ่มเติมอยู่ใน Logs/Codex_RuntimeSafetyFix_Report_20261005.md

### ผลรอบก่อนหน้า: แก้เฉลยข้ามบรรทัดและบททักทายจากค่าที่เพิ่มเอง (5 ตุลาคม)

- EditMode: 252/252 ผ่าน (Logs/Codex_BoundaryFix_EditMode_20261005.xml)
- PlayMode: 34/34 ผ่าน (Logs/Codex_BoundaryFix_PlayMode_20261005.xml)
- เพิ่ม regression ถาวร 29 เคสใน NpcReplyBoundaryTests และ 3 เคสใน NpcReplyBoundaryPlayModeTests รวมเคสจากการตรวจครั้งก่อนและการตรวจว่า mini event ยังเปิดได้ตามค่าหิว/อารมณ์ ชุดเดิม 223/31 เคสยังผ่านทั้งหมด
- Windows standalone: สร้างสำเร็จ 0 errors และ exit code 0 (Logs/Codex_BoundaryFix_Build_20261005.log) ไฟล์ล่าสุด Logs/FrameworkSmoke/MysteryGame.exe ใช้คู่กับโฟลเดอร์ข้อมูลข้างกัน
- ใช้ AI จำลองและปิดการเขียนเซฟ ไม่ทดสอบบัญชี/API จริงหรือภาพ/เสียงด้วยคน การตรวจวลีไม่ได้พิสูจน์ทุกภาษา/ทุก paraphrase รูปแบบเซฟเดิมยังใช้ได้
- Logs เป็นไฟล์ในเครื่อง ไม่อยู่ใน Git ให้ทีมรัน Test Runner และ Validate Content หลัง clone ผลที่เคยพบปัญหายังเก็บเป็นหลักฐานย้อนหลัง ไม่ใช่ผลหลังแก้

ผลด้านล่างเป็นรอบก่อนแก้สองจุดล่าสุด หลังแก้สี่จุดจากการตรวจซ้ำ 5 ตุลาคม:

- EditMode: 223/223 ผ่าน (Logs/Codex_NpcFix_EditMode_20261005.xml)
- PlayMode: 31/31 ผ่าน (Logs/Codex_NpcFix_PlayMode_20261005.xml)
- เพิ่ม regression ถาวร 35 เคสใน NpcConsistencyTests และ 12 เคสใน NpcEventLifecycleTests ชุดเดิม 188/19 เคสยังผ่านทั้งหมด
- Windows standalone: สร้างสำเร็จ 0 errors และจบด้วย exit code 0 (Logs/Codex_NpcFix_Build_20261005.log) ไฟล์ล่าสุด Logs/FrameworkSmoke/MysteryGame.exe ต้องใช้คู่กับโฟลเดอร์ข้อมูลข้างกัน
- ใช้ AI จำลองและปิดการบันทึกลงดิสก์ ไม่เรียก API จริง ไม่เปลี่ยนเซฟผู้เล่น ยังไม่ได้เปิดตรวจภาพ/เสียงในตัวเกมด้วยคน
- การตรวจ protectedTerms เป็นการตรวจข้อความ ไม่ใช่การพิสูจน์ความหมายทุกภาษาหรือทุก paraphrase เนื้อหาที่ต้องแม่นยำให้ใช้ fact canon/บทที่ผู้สร้างเขียน และเพิ่มคำเฉพาะกับเคสทดสอบเมื่อเพิ่มความลับใหม่
- Logs เป็นหลักฐานในเครื่อง ไม่อยู่ใน Git ให้ทีมรัน Test Runner (EditMode/PlayMode), Mystery Game > Validate Content และ Mystery Game > Build Smoke Player อีกครั้งหลัง clone

ผลด้านล่างเป็นรอบก่อนแก้สี่จุดนี้ หลังเพิ่มหลักฐานและเหตุการณ์ต่อเนื่อง 5 ตุลาคม:

- EditMode: 188/188 ผ่าน (Logs/Codex_Evidence_EditMode_Verified_20261005.xml)
- PlayMode: 19/19 ผ่าน (Logs/Codex_Evidence_PlayMode_Verified_20261005.xml) รวมปุ่มหลักฐาน ความรู้ที่ได้เฉพาะเมื่อแบ่งปัน การไม่ให้ผลซ้ำ ปุ่มจากรอบคุยเก่า การล็อกระหว่างรอ AI และเหตุการณ์ที่ปิดก่อนเลือก/เปลี่ยนสถานะก่อนเริ่ม
- Windows standalone: สร้างสำเร็จ 0 errors (Logs/Codex_Evidence_Build_20261005.log) ไฟล์ล่าสุดอยู่ใน Logs/FrameworkSmoke/MysteryGame.exe ต้องใช้คู่กับโฟลเดอร์ข้อมูลข้างกัน ยังไม่ได้เปิดเล่นตรวจภาพ/เสียงด้วยคน
- ใช้ AI จำลองและปิดการบันทึกลงดิสก์ ไม่เรียก API จริง ไม่เปลี่ยนเซฟของผู้เล่น การทดสอบหน้าคุยตรวจพฤติกรรมและสถานะ ไม่ใช่การตรวจภาพ/เสียงด้วยคน

ผลด้านล่างเป็นรอบแก้ปัญหา 5 ตุลาคม ก่อนเพิ่มฟีเจอร์หลักฐาน ให้ทีมรันชุดล่าสุดหลัง clone:

- EditMode: 169/169 ผ่าน (Logs/Codex_Fixes_EditMode_Final_20261005.xml)
- PlayMode: 13/13 ผ่าน (Logs/Codex_Fixes_PlayMode_Verified_20261005.xml) ครอบคลุมปัญหาเดิม การไม่เปิด/แทรกบทลาข้ามรอบสนทนา NPC ที่คุยต่อหลังจบ การแสดง fact จริง การปฏิเสธข้อมูลแต่ง และการตรวจ mini event ซ้ำเมื่อสถานะเปลี่ยน
- สร้าง Windows standalone สำเร็จ 0 errors ด้วยค่าตั้งต้นของโปรเจกต์ (Logs/Codex_Fixes_Build_Verified_20261005.log) ไฟล์ทดลองอยู่ที่ Logs/FrameworkSmoke/MysteryGame.exe โดยต้องใช้คู่กับโฟลเดอร์ข้อมูลที่อยู่ข้างกัน ยังไม่ได้เปิดตรวจภาพ/เสียงในตัวเกมด้วยคน
- ชุดทดสอบเพิ่มอยู่ใน FrameworkTests / FrameworkPlayModeTests และปิดการบันทึกลงดิสก์ ใช้ผู้ให้บริการจำลอง ไม่เรียก API จริงหรือแตะเซฟผู้เล่น

ผลด้านล่างเป็นหลักฐานเดิมรอบ 4 ตุลาคม ก่อนการแก้ปัญหารอบนี้ ให้ทีมรันชุดปัจจุบันหลัง clone:

- EditMode: 147/147 ผ่านจาก Unity จริง (Logs/Codex_Framework_EditMode_Final.xml)
- PlayMode: 5/5 ผ่านจาก Unity จริง (Logs/Codex_Framework_PlayMode_Verified.xml): หน้าคุยบันทึกคำใบ้และปิดได้, รับหนังสือ/พิมพ์ตอบเซนะปลดประตู, โหลดด่านตัวอย่างทั้งสองฉาก, กันเฉลยจากผู้ให้บริการจำลอง และทิ้งคำตอบที่มาหลังปิดแชท
- สร้าง Windows standalone สำเร็จ 0 errors รวมทั้งห้าฉาก (Logs/Codex_Framework_Build.log); ไฟล์ทดลองอยู่ที่ Logs/FrameworkSmoke/MysteryGame.exe ยังไม่ได้ตรวจภาพ/เสียงด้วยการเล่นไฟล์นี้ด้วยคน
- Tests ปิดการบันทึกลงดิสก์และใช้ตัวจำลอง offline ไม่ใช้ API key/โควตา และไม่ทับเซฟผู้เล่น
- ไฟล์ Logs ถูก ignore จึงควรรันซ้ำหลัง clone; มี source tests ใน Assets/Tests ให้ทีมรันเอง
- ยังไม่ได้ยืนยัน API จริงทุกผู้ให้บริการในรอบนี้ และยังต้องเล่นตรวจภาพ เสียง การเดิน และขนาดหน้าต่างด้วยคน
- การกรอง fact ID/คำเฉลยไม่ใช่การรับประกันว่าบทคุยอิสระจะไม่หลอนทางความหมายทุกแบบ คำใบ้ถูกควบคุมแน่นกว่าเพราะไม่ใช้ข้อความที่โมเดลแต่ง
- แยกขอบเขตสถานะ/ผู้ให้บริการแล้ว แต่ DialogueManager ยังรวมการจัดหน้าตาคุยหลายส่วน การเปลี่ยนระบบ UI ทั้งชุดหรือเพิ่มกลไกเกมชนิดใหม่ยังต้องเขียน adapter/โค้ด

งานถัดไปที่เหมาะกับทีม: playtest ด้วยคนครบสามห้อง, ทดสอบ API กับบัญชีที่ใช้จริง, ทำระบบบันทึก cooldown/ตำแหน่งตัวละคร และสร้างชุดภาพเฉพาะเกมที่สอง
