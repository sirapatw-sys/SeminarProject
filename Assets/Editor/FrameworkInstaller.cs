using System;
using System.Collections.Generic;
using System.Linq;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FrameworkInstaller
{
    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
        return asset;
    }
    private static T Create<T>(string path) where T : ScriptableObject
    {
        string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(folder);
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
    private static ConditionRule Flag(string id) { return new ConditionRule { type = ConditionType.HasFlag, targetId = id }; }
    private static ActionCommand Set(string id) { return new ActionCommand { type = ActionType.SetFlag, targetId = id }; }
    private static void Dirty(UnityEngine.Object asset) { EditorUtility.SetDirty(asset); }

    // One-time, explicit migration. Never overwrite a teammate's later authored changes.
    [MenuItem("Mystery Game/Install Framework Data (one time)")]
    public static void Install()
    {
        if (AssetDatabase.LoadAssetAtPath<GameDefinition>("Assets/Resources/GameDefinition.asset") != null)
        { Debug.Log("Framework already installed; existing content preserved."); return; }
        string originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        var items = new List<ItemData>();
        string[] ids = { "key", "paper", "tome", "winding_key" };
        string[] images = { "key_32x32_24f", "paper", "tome", "winding_key" };
        string[] names = { "กุญแจทองเหลือง", "บันทึกของผู้รอดชีวิต", "จารึกที่ยังเขียนมิจบ", "กุญแจไขลาน" };
        string[] descriptions = { "ใช้ไขประตูทางออกของห้องทำงาน",
            "รหัสลิ้นชักคือ 4592 — หยิบกุญแจข้างในแล้วหาทางออก",
            "หนังสือที่เซนะเรียกหาเป็นของถวาย", "ใช้ไขลานกล่องดนตรี" };
        for (int i = 0; i < ids.Length; i++)
        {
            string imagePath = "Assets/Art/Items/" + images[i] + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            var item = Create<ItemData>("Assets/Data/Items/" + ids[i] + ".asset");
            item.itemId = ids[i]; item.displayName = names[i]; item.description = descriptions[i];
            item.image = Load<Sprite>(imagePath); item.horizontalFrames = i == 0 ? 24 : 1;
            Dirty(item); items.Add(item);
        }
        var drawer = Create<InputPuzzleData>("Assets/Data/Puzzles/Drawer_Code.asset");
        drawer.puzzleId = "drawer_code"; drawer.title = "แม่กุญแจรหัสของลิ้นชัก";
        drawer.question = "ใส่รหัส 4 หลักจากบันทึกที่พบ"; drawer.numeric = true;
        drawer.acceptedAnswers = new List<string> { "4592" }; drawer.solvedFlag = "drawer_opened";
        drawer.conditions = new List<ConditionRule> { Flag("found_note") }; Dirty(drawer);
        var interaction = Load<InteractionData>("Assets/Data/Interactions/Drawer_Data.asset");
        interaction.inputPuzzle = drawer; Dirty(interaction);
        var desk = Load<InteractionData>("Assets/Data/Interactions/Desk_Data.asset");
        desk.popupItemId = "paper"; Dirty(desk);
        var door = Load<InteractionData>("Assets/Data/Interactions/DoorR2_Data.asset");
        door.hideFocusUntilAvailable = true; Dirty(door);

        var riddle = Create<InputPuzzleData>("Assets/Data/Puzzles/Sena_Riddle.asset");
        riddle.puzzleId = "sena_riddle"; riddle.title = "ปริศนาของเซนะ";
        riddle.question = "ข้าวิ่งนำหน้าเจ้าอยู่เสมอ ทว่ามิเคยไปถึงแห่งหนใด เจ้าเฝ้ารอข้าไปทั้งชีวิต แต่ครานที่ข้ามาถึงเจ้าจริงๆ ชื่อของข้าก็เปลี่ยนไปเสียแล้ว... ข้าคือสิ่งใด?";
        riddle.translation = "(I am always running ahead of you, yet I never arrive. You spend your life anticipating me, but the moment I reach you, my name has already changed. What am I?)";
        riddle.acceptedAnswers = new List<string> { "พรุ่งนี้", "วันพรุ่งนี้", "วันถัดไป", "วันต่อไป", "วันรุ่งขึ้น",
            "อนาคต", "tomorrow", "next day", "the next day", "future", "the future", "tmr" };
        riddle.conditions = new List<ConditionRule> { Flag("sena_offering_given") };
        riddle.solvedFlag = "sena_passed";
        riddle.successActions = new List<ActionCommand> { Set("room02_door_unlocked"),
            new ActionCommand { type = ActionType.AddHistory, text = "Solved the gatekeeper's riddle" } }; Dirty(riddle);
        var npcPuzzle = Create<NpcPuzzleData>("Assets/Data/NPC/Sena_Puzzle.asset");
        npcPuzzle.npcId = "Sena"; npcPuzzle.requiredItemId = "tome";
        npcPuzzle.offeringGivenFlag = "sena_offering_given"; npcPuzzle.puzzle = riddle;
        npcPuzzle.demandDialogue = Load<DialogueData>("Assets/Data/Dialogue/Sena_Demand.asset");
        npcPuzzle.questionDialogue = Load<DialogueData>("Assets/Data/Dialogue/Sena_Riddle.asset");
        npcPuzzle.meetingActions = new List<ActionCommand> { Set("sena_met"), Set("sena_wants_tome") };
        npcPuzzle.offeringActions = new List<ActionCommand> {
            new ActionCommand { type = ActionType.ChangeRelationship, targetId = "Sena", amount = 3 },
            new ActionCommand { type = ActionType.AddHistory, text = "Gave the gatekeeper an offering" } };
        var farewell = Create<DialogueData>("Assets/Data/Dialogue/Sena_Solved.asset");
        farewell.dialogueId = "sena_solved"; farewell.speakerId = "Sena";
        farewell.speakerName = npcPuzzle.demandDialogue.speakerName;
        farewell.speakerPortrait = npcPuzzle.demandDialogue.speakerPortrait;
        farewell.lines = new List<string> { "...หึ เจ้าตอบถูกจนได้ฤๅ", "ข้ายอมรับในปัญญาของเจ้า ประตูดวงดาวเปิดแล้ว จงก้าวต่อไปเถิด" };
        Dirty(farewell); npcPuzzle.solvedDialogue = farewell; Dirty(npcPuzzle);

        var sena = Load<NpcProfileData>("Assets/Resources/Knowledge/Npcs/Sena.asset");
        var answered = sena.fallbackReplies.Find(rule => rule.ruleId == "answered");
        answered.answerPuzzle = riddle; answered.keywords.Clear();
        foreach (var note in sena.situationalNotes)
            if (note.note.Contains("คำตอบที่ถือว่าถูก"))
                note.note = "รับของถวายแล้ว เอ่ยปริศนาไปแล้ว อย่าเฉลยหรือเดาคำตอบให้ผู้เล่น เกมเป็นผู้ตรวจคำตอบ ไม่ใช่ AI";
        Dirty(sena);
        var rooms = new List<RoomKnowledgeData>();
        for (int i = 1; i <= 3; i++)
        {
            var room = Load<RoomKnowledgeData>("Assets/Resources/Knowledge/Rooms/Room0" + i + ".asset");
            room.enteredFlag = "room0" + i + "_entered";
            if (i == 1) room.FindFact("drawer_code").protectedTerms = new List<string> { "4592" };
            if (i == 2) room.FindFact("riddle_answer").protectedTerms = riddle.acceptedAnswers.ToList();
            if (i == 3) room.FindFact("key_in_ashes").protectedTerms = new List<string> { "ในขี้เถ้า", "ในเตาผิง" };
            Dirty(room); rooms.Add(room);
        }
        foreach (string id in new[] { "Alice", "Rina", "Stelle", "Sena" })
        {
            var ev = Load<MiniEventData>("Assets/Data/Events/" + id + "_Chatter_Event.asset");
            ev.offlineVariants.Clear();
            string[] topics = id == "Sena" ? new[] { "รออยู่ที่นี่นานนักแล้ว เจ้ารู้สึกอย่างไรกับความเงียบนี้?", "มนุษย์เอ๋ย เจ้ายังจำบ้านของเจ้าได้หรือไม่?" } :
                new[] { "อยู่ในนี้นานแล้ว เราเริ่มคิดถึงบ้าน เธอเคยรู้สึกแบบนี้ไหม?", "ความเงียบในนี้ทำให้เรากังวล อยู่คุยเป็นเพื่อนสักหน่อยได้ไหม?" };
            for (int i = 0; i < topics.Length; i++)
            {
                var variant = Create<DialogueData>("Assets/Data/Dialogue/" + id + "_OfflineTopic" + i + ".asset");
                EditorUtility.CopySerialized(ev.dialogue, variant);
                variant.dialogueId = id.ToLowerInvariant() + "_offline_" + i;
                variant.lines = new List<string> { topics[i] };
                for (int j = 0; j < variant.choices.Count; j++)
                {
                    variant.choices[j].optionText = j == 0 ? "เล่าให้เราฟังได้นะ" : j == 1 ? "อืม เราฟังอยู่" : "ตอนนี้เราไม่อยากคุย";
                    variant.choices[j].responseText = j == 0 ? "ขอบใจนะ แค่มีคนฟังก็ช่วยแล้ว" : j == 1 ? "อย่างน้อยเราก็ไม่ได้อยู่คนเดียว" : "ได้... ไว้คุยกันทีหลัง";
                }
                Dirty(variant); ev.offlineVariants.Add(variant);
            }
            Dirty(ev);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/Room02.unity");
        var component = UnityEngine.Object.FindObjectOfType<NpcPuzzleInteraction>();
        if (component == null) throw new InvalidOperationException("Room02 lost its puzzle component.");
        var serialized = new SerializedObject(component);
        serialized.FindProperty("definition").objectReferenceValue = npcPuzzle;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(component.gameObject.scene);

        var game = Create<GameDefinition>("Assets/Resources/GameDefinition.asset");
        game.gameId = "mystery"; game.title = "AI Mystery Escape Room · บทที่ 1";
        game.rooms = rooms; game.items = items;
        game.npcs = new[] { "Alice", "Sena", "Rina", "Stelle" }.Select(id =>
            Load<NpcProfileData>("Assets/Resources/Knowledge/Npcs/" + id + ".asset")).ToList();
        game.introLines = new[] { "ค.ศ. 2001", "ผมเองก็อธิบายไม่ได้ว่ามาอยู่ที่นี่ได้ยังไง",
            "คืนนั้นผมกลับถึงบ้าน วางกุญแจไว้บนโต๊ะ\nแล้วเผลอหลับไปทั้งที่ยังไม่ได้ถอดรองเท้า",
            "...นั่นคือเรื่องสุดท้ายที่ผมจำได้",
            "พอลืมตาขึ้นมาอีกที ก็ไม่มีประตูที่ผมเดินเข้ามาแล้ว\nไม่มีเสียงจากข้างนอก ไม่รู้ด้วยซ้ำว่าตอนนี้วันไหน เวลาเท่าไหร่",
            "มีแค่ห้องห้องหนึ่ง\nที่ดูเหมือนจะจำหน้าผมได้ ดีกว่าที่ผมจำมันได้เสียอีก" };
        Dirty(game);
        AssetDatabase.SaveAssets();
        CreateSample(game);
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrWhiteSpace(originalScene)) EditorSceneManager.OpenScene(originalScene);
        Debug.Log("Framework data, generic puzzle migration and two-room sample installed.");
    }

    private static void Wire(UnityEngine.Object component, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(component);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    // Used to repair only this generated sample during installation/testing.
    public static void RebuildSample()
    {
        CreateSample(Load<GameDefinition>("Assets/Resources/GameDefinition.asset"));
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/Room01.unity");
    }
    private static void CreateSample(GameDefinition main)
    {
        const string root = "Assets/Samples/Observatory/";
        var game = Create<GameDefinition>(root + "Observatory.asset");
        game.gameId = "observatory_sample"; game.title = "Observatory · Framework Sample";
        game.firstScene = "ObservatoryA"; game.initialGoal = "restore_beacon";
        game.introLines = new[] { "หอสังเกตการณ์เงียบลง", "เราต้องเปิดประตูห้องควบคุม แล้วจุดสัญญาณก่อนรุ่งเช้า" };
        var npc = Create<NpcProfileData>(root + "Nora.asset");
        npc.npcId = "Nora"; npc.displayName = "โนรา"; npc.persona = "ช่างสังเกต ใจเย็น เป็นผู้ดูแลหอสังเกตการณ์";
        npc.portrait = main.npcs[0].portrait; // Placeholder art is explicitly reused.
        npc.neutralReplies = new List<string> { "เราจะหาทางออกไปด้วยกัน", "อย่าหมดกำลังใจนะ" };
        npc.tones = new List<RelationshipTone> { new RelationshipTone { upTo = 100, maxHintLevel = HintLevel.Explicit, tone = "สุภาพ สงบ" } };
        var dialogue = Create<DialogueData>(root + "Nora_Intro.asset");
        dialogue.dialogueId = "nora_intro"; dialogue.speakerId = npc.npcId; dialogue.speakerName = npc.displayName;
        dialogue.speakerPortrait = npc.portrait; dialogue.lines = new List<string> { "เราคือโนรา ถ้าต้องการความช่วยเหลือ ถามเราได้" };
        var clue = Create<InteractionData>(root + "Clue.asset");
        clue.interactionId = "observatory_clue"; clue.displayName = "แผ่นบันทึก";
        clue.interactionMessage = "รหัสห้องควบคุมคือ 2468"; clue.repeatable = false;
        clue.actions = new List<ActionCommand> { Set("observatory_clue_read") };
        var puzzle = Create<InputPuzzleData>(root + "Lock.asset");
        puzzle.puzzleId = "observatory_lock"; puzzle.title = "ล็อคห้องควบคุม"; puzzle.question = "ใส่รหัสจากแผ่นบันทึก";
        puzzle.numeric = true; puzzle.acceptedAnswers = new List<string> { "2468" };
        puzzle.solvedFlag = "observatory_lock_open"; puzzle.conditions = new List<ConditionRule> { Flag("observatory_clue_read") };
        var lockData = Create<InteractionData>(root + "Door.asset");
        lockData.interactionId = "observatory_open"; lockData.displayName = "ประตูห้องควบคุม";
        lockData.inputPuzzle = puzzle; lockData.conditions = new List<ConditionRule> { Flag("observatory_clue_read") };
        lockData.actions = new List<ActionCommand> { Set("observatory_lock_open") };
        lockData.repeatable = false; lockData.interactionMessage = "ประตูเปิดแล้ว"; lockData.transitionScene = "ObservatoryB";
        lockData.transitionMessage = "เข้าสู่ห้องควบคุม";
        var beacon = Create<InteractionData>(root + "Beacon.asset");
        beacon.interactionId = "observatory_beacon"; beacon.displayName = "เครื่องส่งสัญญาณ";
        beacon.interactionMessage = "สัญญาณสว่างขึ้น เราปลอดภัยแล้ว"; beacon.endsDemo = true; beacon.repeatable = false;
        beacon.transitionMessage = "โนราและคุณส่งสัญญาณสำเร็จ";
        beacon.actions = new List<ActionCommand> { Set("observatory_beacon_lit") };
        game.npcs = new List<NpcProfileData> { npc }; game.items.Clear();
        foreach (var asset in new UnityEngine.Object[] { game, npc, dialogue, clue, puzzle, lockData, beacon }) Dirty(asset);
        AssetDatabase.SaveAssets();
        game.rooms.Clear();
        for (int i = 0; i < 2; i++)
        {
            string id = i == 0 ? "ObservatoryA" : "ObservatoryB";
            var room = Create<RoomKnowledgeData>(root + id + ".asset");
            room.roomId = id; room.roomName = i == 0 ? "หอสังเกตการณ์" : "ห้องควบคุม";
            room.roomDescription = "ตัวอย่างการต่อยอดด้วยข้อมูล ใช้ภาพเดิมเป็นภาพชั่วคราว";
            room.background = main.rooms[0].background; room.enteredFlag = id + "_entered";
            room.steps = i == 0 ? new List<PuzzleStep> {
                new PuzzleStep { stepId = "read_clue", interactionId = clue.interactionId, completedWhen = new List<ConditionRule> { Flag("observatory_clue_read") },
                    vagueHint = "มีอะไรเขียนไว้ในห้อง", normalHint = "ตรวจแผ่นบันทึก", explicitHint = "อ่านแผ่นบันทึกที่จุดภาพวาด" },
                new PuzzleStep { stepId = "open_control", interactionId = lockData.interactionId,
                    availableWhen = new List<ConditionRule> { Flag("observatory_clue_read") },
                    completedWhen = new List<ConditionRule> { Flag("observatory_lock_open") },
                    vagueHint = "ตัวเลขน่าจะเกี่ยวกับทางออก", normalHint = "ใช้รหัสจากบันทึกที่ประตู", explicitHint = "กรอกรหัส 2468 ที่ประตู" }
            } : new List<PuzzleStep> { new PuzzleStep { stepId = "light_beacon", interactionId = beacon.interactionId,
                completedWhen = new List<ConditionRule> { Flag("observatory_beacon_lit") },
                vagueHint = "ยังมีสิ่งที่ต้องเปิด", normalHint = "เปิดเครื่องส่งสัญญาณ", explicitHint = "กด E ที่เครื่องส่งสัญญาณตรงภาพวาด" } };
            room.facts = i == 0 ? new List<RoomFact> { new RoomFact { factId = "observatory_code", statement = "รหัสคือ 2468",
                isPuzzleAnswer = true, protectedTerms = new List<string> { "2468" }, revealedWhen = new List<ConditionRule> { Flag("observatory_clue_read") } } } : new List<RoomFact>();
            Dirty(room); game.rooms.Add(room);
            Dirty(game);
            AssetDatabase.SaveAssets();
            string scenePath = root + id + ".unity";
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Room01.unity");
            // OpenScene may unload assets that no current scene references; reload saved data.
            game = Load<GameDefinition>(root + "Observatory.asset");
            dialogue = Load<DialogueData>(root + "Nora_Intro.asset");
            clue = Load<InteractionData>(root + "Clue.asset");
            lockData = Load<InteractionData>(root + "Door.asset");
            beacon = Load<InteractionData>(root + "Beacon.asset");
            new GameObject("GameDefinition").AddComponent<GameDefinitionSelector>().definition = game;
            foreach (var obj in UnityEngine.Object.FindObjectsOfType<ObjectInteraction>())
            {
                if (obj.name == "Painting") Wire(obj, "interactionData", i == 0 ? clue : beacon);
                else if (i == 0 && obj.name == "Door") Wire(obj, "interactionData", lockData);
                else UnityEngine.Object.DestroyImmediate(obj.gameObject);
            }
            foreach (var actor in UnityEngine.Object.FindObjectsOfType<NPCInteraction>())
            {
                actor.name = "Nora"; Wire(actor, "dialogueData", dialogue);
                foreach (var ev in actor.GetComponents<NpcEventController>()) UnityEngine.Object.DestroyImmediate(ev);
                foreach (var needs in actor.GetComponents<NpcNeedController>()) UnityEngine.Object.DestroyImmediate(needs);
                if (i == 0) { Folder("Assets/Prefabs"); PrefabUtility.SaveAsPrefabAsset(actor.gameObject, "Assets/Prefabs/ExampleNpc.prefab"); }
            }
            EditorSceneManager.SaveScene(scene, scenePath);
        }
        var scenes = EditorBuildSettings.scenes.ToList();
        foreach (string id in new[] { "ObservatoryA", "ObservatoryB" })
            if (!scenes.Any(s => s.path == root + id + ".unity"))
                scenes.Add(new EditorBuildSettingsScene(root + id + ".unity", true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
