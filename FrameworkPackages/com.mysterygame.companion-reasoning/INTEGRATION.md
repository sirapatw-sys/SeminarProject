# Integrating Companion Reasoning 0.2
Verified target: Unity 2021.3.16f1. No AI provider or API key is required.
Start with README.md and import Repair Workshop before adapting your own game.

## Ownership
| Package | Your game (host) |
| --- | --- |
| Authored profile/rules and bounded query matching | Input, dialogue UI and localization |
| Evaluate evidence and permitted detail | Actually observed and currently disclosable facts |
| Return line/outcome/referenced fact IDs | Relationship-to-detail mapping |
| Register named effect handlers | Validated interactions, movement, cancellation, rewards, saves |
| Configuration validation | Content review, puzzle truth and policy enforcement |

The profile is authored reasoning, not a general LLM planner. Changing a profile changes
language and rules; the host must provide matching fact/flag IDs and implement mechanics.

## Connect a request
1. Create a ReasoningProfile from Assets > Create > Companion Framework > Reasoning Profile.
2. Set npcId, safe fallback lines and a shared cautiousLine. Add rules with unique IDs,
   phrases, requiredFacts, optional requiredFlags and excludedFlags.
3. Implement IReasoningWorld for a fresh snapshot of this NPC's currently permitted knowledge.
   Do not pass every world fact simply because the player knows it.
4. On the player's verified send, call ReasoningQueryMatcher.Read(profile, text).
5. Handle Kind None as ordinary dialogue. For Report, render only host-approved shareable
   evidence; the matcher is not a fact database or a report renderer.
6. For Hypothesis, require exactly one matched rule; otherwise use clarifyLine.
   Call profile.Evaluate(rule, world). Map Unavailable to unavailableLine and an empty result
   to insufficientEvidenceLine. A Consideration is discussion, never task completion.
7. Render the returned line with your UI. Only fact IDs your host currently permits may be shown.
8. Register physical effects once; call TryExecute only from a host-validated interaction,
   never directly from a model proposal or a reasoning reply.

AllowedDetail <= 0 makes the rule unavailable; 1 uses the shared cautious line,
2 permits the rule's normal line, 3 permits its detailed line. Missing evidence uses
insufficientLine. Author these lines so a low-detail or missing-evidence reply cannot leak
a higher-detail answer. The engine cannot prove arbitrary authored prose is spoiler-free.
Use profile.Evaluate rather than calling the lower-level engine with rule-specific cautious
lines if different hypotheses must remain indistinguishable at low trust.

## Change theme: greenhouse
Host facts: soil_dry, water_available. Flag: plant_watered.
Profile hypothesis phrase: "maybe the plant needs water".
Required facts: both above. Excluded flag: plant_watered.
Cautious line: "We have observations; let's check before choosing what to do."
Normal line: "Those observations support considering water. You should test the idea."
Insufficient line: "I do not have enough observations to assess that yet."

Your host exposes actual observations through IReasoningWorld. Register water-plant
with a handler that rechecks the facts, player range, interaction state and plant_watered,
then updates the host once. Writing or evaluating the hypothesis must not invoke it.
RepairWorkshopCompanion is an example host with device-specific IDs; changing only its
profile does not reconfigure its Inspect/Repair buttons to greenhouse mechanics.

## Validate your integration
- Before either fact: safe insufficient evidence, no side effects.
- Both facts present: detail 1/2/3 follow authored disclosure; no completion.
- Another NPC's/private/expired evidence: excluded by the host snapshot.
- Completed flag: unavailable; the NPC need not pretend to forget known facts.
- Unknown/ambiguous hypothesis: ask back, do not guess.
- Physical interaction: succeeds once after all host checks; repeated/unknown effects fail.
- Save/load: host restores facts/flags, then creates a fresh snapshot for the next reply.
- Review configuration errors in the Inspector. Run package tests in Test Runner.

## Troubleshooting and distribution
Missing sample: import it from Package Manager after installing package.json from disk.
Missing match: inspect profile phrases/prefixes/suffixes and avoid claiming unrestricted language.
Unexpected information: inspect the host snapshot, detail limit and authored lines.
Action never runs: the package does not dispatch dialogue as gameplay; wire a real host interaction.
The package contains no Mystery Game scenes, animation, save migration or AI service.
Distribution license for this repository has not been declared in this package; the owning
team must decide code rights and review third-party assets before public redistribution.
