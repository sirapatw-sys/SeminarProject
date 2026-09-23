using System.Collections.Generic;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEditor;

namespace MysteryGame.Tests
{
    /// <summary>
    /// The puzzle is authored twice: once as InteractionData (what the objects
    /// actually do) and once as RoomKnowledgeData.steps (what the NPCs think
    /// the next step is). These tests fail the moment the two drift apart.
    /// </summary>
    public class InteractionConsistencyTests
    {
        private static readonly string[] Rooms = { "Room01", "Room02", "Room03" };

        private static Dictionary<string, InteractionData> LoadInteractions()
        {
            Dictionary<string, InteractionData> byId = new Dictionary<string, InteractionData>();
            foreach (string guid in AssetDatabase.FindAssets("t:InteractionData", new[] { "Assets/Data/Interactions" }))
            {
                InteractionData data = AssetDatabase.LoadAssetAtPath<InteractionData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(data, Is.Not.Null);
                Assert.That(byId.ContainsKey(data.interactionId), Is.False,
                            "two InteractionData share the id " + data.interactionId);
                byId[data.interactionId] = data;
            }

            return byId;
        }

        private static bool SameRule(ConditionRule a, ConditionRule b)
        {
            return a.type == b.type && a.targetId == b.targetId && a.amount == b.amount;
        }

        /// <summary>Flags the engine raises itself rather than an interaction.</summary>
        private static bool IsEngineFlag(ConditionRule rule)
        {
            return rule.type == ConditionType.HasFlag && rule.targetId.EndsWith("_entered");
        }

        private static bool Produces(InteractionData data, ConditionRule rule)
        {
            foreach (ActionCommand action in data.actions)
            {
                if (rule.type == ConditionType.HasFlag &&
                    action.type == ActionType.SetFlag && action.targetId == rule.targetId)
                {
                    return true;
                }

                if (rule.type == ConditionType.HasItem &&
                    action.type == ActionType.AddItem && action.targetId == rule.targetId)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void EveryLinkedStepPointsAtARealInteraction()
        {
            Dictionary<string, InteractionData> interactions = LoadInteractions();
            foreach (string roomId in Rooms)
            {
                foreach (PuzzleStep step in KnowledgeLibrary.GetRoom(roomId).steps)
                {
                    if (string.IsNullOrEmpty(step.interactionId))
                    {
                        continue;
                    }

                    Assert.That(interactions.ContainsKey(step.interactionId), Is.True,
                                roomId + "/" + step.stepId + " links to missing interaction " +
                                step.interactionId);
                }
            }
        }

        [Test]
        public void AStepIsOnlyAvailableWhenItsInteractionWouldSucceed()
        {
            Dictionary<string, InteractionData> interactions = LoadInteractions();
            foreach (string roomId in Rooms)
            {
                foreach (PuzzleStep step in KnowledgeLibrary.GetRoom(roomId).steps)
                {
                    InteractionData data;
                    if (string.IsNullOrEmpty(step.interactionId) ||
                        !interactions.TryGetValue(step.interactionId, out data))
                    {
                        continue;
                    }

                    // The NPC must not send the player to an object that will
                    // still refuse them: each gate the step waits for has to be
                    // a condition the interaction checks too, and vice versa.
                    foreach (ConditionRule rule in step.availableWhen)
                    {
                        Assert.That(data.conditions.Exists(c => SameRule(c, rule)), Is.True,
                                    roomId + "/" + step.stepId + " waits for " + rule.type + " " +
                                    rule.targetId + " but " + data.name + " does not check it");
                    }

                    foreach (ConditionRule rule in data.conditions)
                    {
                        Assert.That(step.availableWhen.Exists(c => SameRule(c, rule)), Is.True,
                                    data.name + " requires " + rule.type + " " + rule.targetId +
                                    " but step " + roomId + "/" + step.stepId + " offers it without that");
                    }
                }
            }
        }

        [Test]
        public void DoingTheInteractionCompletesTheStep()
        {
            Dictionary<string, InteractionData> interactions = LoadInteractions();
            foreach (string roomId in Rooms)
            {
                foreach (PuzzleStep step in KnowledgeLibrary.GetRoom(roomId).steps)
                {
                    InteractionData data;
                    if (string.IsNullOrEmpty(step.interactionId) ||
                        !interactions.TryGetValue(step.interactionId, out data))
                    {
                        continue;
                    }

                    foreach (ConditionRule rule in step.completedWhen)
                    {
                        if (IsEngineFlag(rule))
                        {
                            continue;
                        }

                        Assert.That(Produces(data, rule), Is.True,
                                    data.name + " never produces " + rule.type + " " + rule.targetId +
                                    ", so " + roomId + "/" + step.stepId + " could never complete");
                    }
                }
            }
        }

        [Test]
        public void EveryRoomAfterTheFirstIsReachedThroughADoor()
        {
            Dictionary<string, InteractionData> interactions = LoadInteractions();
            HashSet<string> targets = new HashSet<string>();
            bool endsDemo = false;
            foreach (InteractionData data in interactions.Values)
            {
                if (!string.IsNullOrEmpty(data.transitionScene))
                {
                    targets.Add(data.transitionScene);
                }
                endsDemo |= data.endsDemo;
            }

            Assert.That(targets, Does.Contain("Room02"));
            Assert.That(targets, Does.Contain("Room03"));
            Assert.That(endsDemo, Is.True, "the last room needs a door that ends the demo");
        }

        [Test]
        public void EveryRoomHasItsOwnBackgroundAndSound()
        {
            HashSet<UnityEngine.Sprite> backgrounds = new HashSet<UnityEngine.Sprite>();
            foreach (string roomId in Rooms)
            {
                RoomKnowledgeData room = KnowledgeLibrary.GetRoom(roomId);
                Assert.That(room.background, Is.Not.Null, roomId + " has no background");
                Assert.That(backgrounds.Add(room.background), Is.True,
                            roomId + " borrows another room's background");
                Assert.That(room.sounds.Count > 0 || room.hauntedDrone, Is.True, roomId + " is silent");
                foreach (RoomSound sound in room.sounds)
                {
                    Assert.That(sound.clip, Is.Not.Null, roomId + " has an empty sound layer");
                }
            }
        }

        [Test]
        public void TheMusicBoxPlaysWhenItIsWound()
        {
            InteractionData wind = LoadInteractions()["wind_music_box"];
            Assert.That(wind.successClip, Is.Not.Null);
        }

        [Test]
        public void EveryMiniEventHasADialogueWithTheRightShape()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:MiniEventData", new[] { "Assets/Data/Events" }))
            {
                MiniEventData data = AssetDatabase.LoadAssetAtPath<MiniEventData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(data.dialogue, Is.Not.Null, data.name + " has no dialogue");
                Assert.That(data.dialogue.choices.Count, Is.InRange(1, 4),
                            data.name + " needs 1-4 choices for the choice panel");
                if (data.triggerType == MiniEventTriggerType.RoomProgress)
                {
                    bool found = false;
                    foreach (string roomId in Rooms)
                    {
                        found |= KnowledgeLibrary.GetRoom(roomId).steps.Exists(s => s.stepId == data.metricId);
                    }
                    Assert.That(found, Is.True, data.name + " waits for unknown step " + data.metricId);
                }
            }
        }
    }
}
