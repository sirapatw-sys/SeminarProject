# Companion Reasoning 0.1

This is a reusable **evidence reasoning module**, not the entire Mystery Game runtime.
It has no dependency on that game's scenes, Resources, GameState, movement, saves, UI or AI provider.
Unity 2021.3 is the initial verification target.

## Install in another Unity project

1. Copy this whole package directory to a permanent location.
2. In Package Manager choose Add package from disk and select package.json.
3. Import Repair Workshop from Samples. Add RepairWorkshopCompanion to an empty GameObject.
4. Assign the included WorkshopProfile asset. Enter Play.
5. Type "maybe the cable is loose" before and after inspecting the cable and power switch.
6. Change allowedDetail between 1, 2 and 3 in the Inspector. Dialogue never repairs the device.
7. Try the physical repair interaction separately; it can succeed once.

Create another profile with Assets > Create > Companion Framework > Reasoning Profile.
Set npcId, phrases, requiredFacts and the cautious/normal/detailed/insufficient lines.
Assign it to the sample component to try a different NPC without changing the engine.
The sample's buttons inspect/repair a device; they do not select commands to the NPC.

## Host integration and limits

Implement IReasoningWorld with the NPC's actually known, currently permitted facts,
world flags, and disclosure limit. Evaluate an authored rule on each request.
Consideration means a discussion of evidence, never proof that a hypothesis or task succeeded.
Phrase classification, physical actions, saves, rewards and memories remain host responsibilities.

CompanionEffectRegistry lets the host register a mechanic without editing a package enum/switch.
Invoke registered IDs only after verifying the player's interaction and context.
Do not dispatch IDs directly from model output. Unknown IDs fail closed.

Author low-detail lines that cannot reveal higher-tier information.
An empty cautious line cannot fall forward to a higher-tier line.
An insufficient-evidence line must not reveal any required fact that the NPC lacks.

Room01 integrates this module through CompanionReasoning. Moving that whole game to another
project still requires its existing host adapters and assets; this package alone does not
provide Mystery Game movement, dialogue UI, AI providers, or save compatibility.

## Reproduce independent-project checks

From the repository run Tools/Test-CompanionPackage.ps1 (optional -UnityPath).
It copies the minimal project template and sample into Logs/CompanionSmoke,
then runs package EditMode tests and the sample's PlayMode smoke test.
That generated project contains none of the main game's Assets or assemblies.
The template is test scaffolding; distribute the package folder to consumers.
