using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    /// <summary>
    /// When triggers overlap, E goes to the one nearest the player's feet,
    /// never to both and never to one that is switched off.
    /// </summary>
    public class InteractionFocusTests
    {
        private class Fake : IFocusable
        {
            public bool CanFocus { get; set; } = true;
            public string FocusPrompt { get; set; } = "";
            public Vector2 FocusPoint { get; set; }
            public void Interact() { }
        }

        [Test]
        public void TheNearestInteractableWins()
        {
            Fake chest = new Fake { FocusPoint = new Vector2(-2.5f, -3.2f) };
            Fake alice = new Fake { FocusPoint = new Vector2(-1.2f, -3.3f) };
            List<IFocusable> both = new List<IFocusable> { alice, chest };

            Assert.That(InteractionFocus.PickNearest(both, new Vector2(-2.4f, -3.0f)), Is.SameAs(chest));
            Assert.That(InteractionFocus.PickNearest(both, new Vector2(-1.4f, -3.0f)), Is.SameAs(alice));
        }

        [Test]
        public void SwitchedOffInteractablesAreSkipped()
        {
            Fake gate = new Fake { FocusPoint = Vector2.zero, CanFocus = false };
            Fake sena = new Fake { FocusPoint = new Vector2(0f, 1f) };

            Assert.That(InteractionFocus.PickNearest(new List<IFocusable> { gate, sena }, Vector2.zero), Is.SameAs(sena));
            Assert.That(InteractionFocus.PickNearest(new List<IFocusable> { gate }, Vector2.zero), Is.Null);
            Assert.That(InteractionFocus.PickNearest(new List<IFocusable>(), Vector2.zero), Is.Null);
        }
    }
}
