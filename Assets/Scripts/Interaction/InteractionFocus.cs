using System.Collections.Generic;
using UnityEngine;

/// <summary>Something the player can walk up to and press E on.</summary>
public interface IFocusable
{
    /// <summary>False while it should be ignored (the gate while Sena guards it).</summary>
    bool CanFocus { get; }

    string FocusPrompt { get; }

    /// <summary>World point "nearness" is measured to: the object, or the NPC's feet.</summary>
    Vector2 FocusPoint { get; }

    void Interact();
}

/// <summary>
/// Triggers overlap: an NPC standing by a chest, two shelf sections side by
/// side. Only the one nearest the player's feet shows its prompt and answers
/// E, so a press never goes to whichever script happened to update first.
/// </summary>
public class InteractionFocus : MonoBehaviour
{
    private static readonly List<IFocusable> inRange = new List<IFocusable>();
    private static InteractionFocus instance;
    private static string shownPrompt = string.Empty;

    private Collider2D playerFeet;

    /// <summary>What E would act on right now, or null.</summary>
    public static IFocusable Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject go = new GameObject("InteractionFocus");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<InteractionFocus>();
    }

    public static void Enter(IFocusable focusable)
    {
        if (focusable != null && !inRange.Contains(focusable))
        {
            inRange.Add(focusable);
        }
    }

    public static void Exit(IFocusable focusable)
    {
        inRange.Remove(focusable);
    }

    /// <summary>The focusable nearest to <paramref name="feet"/>, or null.</summary>
    public static IFocusable PickNearest(IList<IFocusable> candidates, Vector2 feet)
    {
        IFocusable best = null;
        float bestDistance = float.MaxValue;
        foreach (IFocusable candidate in candidates)
        {
            if (candidate == null || !candidate.CanFocus)
            {
                continue;
            }

            float distance = (candidate.FocusPoint - feet).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private void Update()
    {
        // Destroyed or disabled objects never send OnTriggerExit2D.
        inRange.RemoveAll(IsGone);

        Current = PickNearest(inRange, FeetPosition());
        ShowPrompt(Current != null ? Current.FocusPrompt : string.Empty);

        if (Current != null && !InputGate.IsBlocked && Input.GetKeyDown(KeyCode.E))
        {
            Current.Interact();
        }
    }

    private static bool IsGone(IFocusable focusable)
    {
        Behaviour behaviour = focusable as Behaviour;
        return behaviour == null || !behaviour.isActiveAndEnabled;
    }

    private Vector2 FeetPosition()
    {
        if (playerFeet == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            playerFeet = player != null ? player.GetComponent<Collider2D>() : null;
        }

        return playerFeet != null ? (Vector2)playerFeet.bounds.center : Vector2.zero;
    }

    private static void ShowPrompt(string desired)
    {
        if (!string.IsNullOrEmpty(desired))
        {
            // Also puts it back after a dialogue or a message cleared it.
            if (AiSettingsPanel.InteractionPrompt != desired)
            {
                AiSettingsPanel.SetInteractionPrompt(desired);
            }
        }
        else if (!string.IsNullOrEmpty(shownPrompt))
        {
            AiSettingsPanel.ClearInteractionPrompt(shownPrompt);
        }

        shownPrompt = desired ?? string.Empty;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            inRange.Clear();
            Current = null;
        }
    }
}
