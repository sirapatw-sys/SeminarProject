# Companion Reasoning 0.2

This is a reusable **evidence reasoning module**, not the entire Mystery Game runtime.
It has no dependency on that game's scenes, Resources, GameState, movement, saves, UI or AI provider.
Unity 2021.3 is the initial verification target.

After the sample, follow [INTEGRATION.md](INTEGRATION.md) for ownership, host wiring,
theme changes, disclosure rules and troubleshooting. Package distribution rights
have not yet been declared; confirm them with the owning team before public redistribution.

## Install in another Unity project

1. Copy this whole package directory to a permanent location.
2. In Package Manager choose Add package from disk and select package.json.
3. Import Repair Workshop from Samples. Add RepairWorkshopCompanion to an empty GameObject.
4. Assign the included WorkshopProfile asset. Enter Play.
5. Type "maybe the cable is loose" before and after inspecting the cable and power switch.
6. Change allowedDetail between 1, 2 and 3 in the Inspector. Dialogue never repairs the device.
7. Try the physical repair interaction separately; it can succeed once.

Create another profile with Assets > Create > Companion Framework > Reasoning Profile.
Set npcId, phrases, requiredFacts, the profile's shared cautiousLine, and the rules'
normal/detailed/insufficient lines. The Inspector reports missing and ambiguous configuration.
Assign it to the sample component to try a different NPC without changing the engine.
The sample's buttons inspect/repair a device; they do not select commands to the NPC.

## Host integration and limits

Implement IReasoningWorld with the NPC's actually known, currently permitted facts,
world flags, and disclosure limit. Use profile.Evaluate(rule, world) on each request
so low-disclosure replies use the same cautious line across hypotheses.
Consideration means a discussion of evidence, never proof that a hypothesis or task succeeded.
ReasoningQueryMatcher provides bounded phrase matching, polite endings and configurable
report phrases, hypothesis prefixes/suffixes and topicTerms. Unknown hypotheses about
configured puzzle topics ask back; unrelated social thoughts remain ordinary conversation.
It does not remove negation or extra actions to force a known match.
Physical actions, saves, rewards and memories remain host responsibilities.

CompanionEffectRegistry lets the host register a mechanic without editing a package enum/switch.
Invoke registered IDs only after verifying the player's interaction and context.
Do not dispatch IDs directly from model output. Unknown IDs fail closed.

Author low-detail lines that cannot reveal higher-tier information.
Use one neutral cautiousLine for a profile so comparing replies cannot reveal which
hypothesis is correct at low trust. The lower-level engine overload still accepts
rule.cautiousLine for compatibility; direct callers must enforce the same disclosure policy.
An empty cautious line cannot fall forward to a higher-tier line.
An insufficient-evidence line must not reveal any required fact that the NPC lacks.
Unavailable is a separate response from missing evidence: a completed/excluded hypothesis
must not make the companion pretend to have forgotten known evidence.

Room01 integrates this module through CompanionReasoning. Moving that whole game to another
project still requires its existing host adapters and assets; this package alone does not
provide Mystery Game movement, dialogue UI, AI providers, or save compatibility.

## Reproduce independent-project checks

From the repository run Tools/Test-CompanionPackage.ps1 (optional -UnityPath).
It copies the minimal project template and sample into Logs/CompanionSmoke,
then runs package EditMode tests and the sample's PlayMode smoke test.
That generated project contains none of the main game's Assets or assemblies.
The template is test scaffolding; distribute the package folder to consumers.
