using System.Collections;
using CompanionFramework;
using CompanionFramework.Samples;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class FreshProjectSmokeTests
{
    [UnityTest]
    public IEnumerator WorkshopRunsWithOnlyDeclaredPackageAndSample()
    {
        var go = new GameObject("IndependentWorkshop");
        try
        {
            var actor = go.AddComponent<RepairWorkshopCompanion>();
            actor.profile = AssetDatabase.LoadAssetAtPath<ReasoningProfile>("Assets/Workshop/WorkshopProfile.asset");
            Assert.That(actor.profile, Is.Not.Null);
            Assert.That(actor.Reply("maybe the cable is loose"), Does.Contain("Inspect"));
            Assert.That(actor.RepairDevice(), Is.False);
            actor.observedFacts.Add("loose_cable"); actor.observedFacts.Add("power_off");
            actor.allowedDetail = 1;
            Assert.That(actor.Reply("maybe the cable is loose"), Does.Contain("observations"));
            actor.allowedDetail = 3;
            Assert.That(actor.Reply("maybe the cable is loose"), Does.Contain("test it yourself"));
            Assert.That(actor.repairedDevices, Is.Zero, "Dialogue must not perform the physical action.");
            Assert.That(actor.RepairDevice(), Is.True);
            Assert.That(actor.RepairDevice(), Is.False);
            Assert.That(actor.repairedDevices, Is.EqualTo(1));
            Assert.That(actor.Reply("maybe the cable is loose"), Is.EqualTo(actor.profile.unavailableLine));
            yield return null;
        }
        finally { Object.DestroyImmediate(go); }
    }
}
