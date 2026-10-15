using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Readable dialogue at a fixed font size, with wheel/drag and a visible scrollbar.</summary>
public sealed class DialogueTextScroll : MonoBehaviour
{
    private TMP_Text text;
    private ScrollRect scroll;
    private RectTransform viewport;
    private string previousText;
    private Vector2 previousSize;

    public static void Install(TMP_Text target)
    {
        if (target == null || target.GetComponentInParent<DialogueTextScroll>(true) != null) return;
        RectTransform original = target.rectTransform;
        var host = new GameObject("DialogueTextScroll", typeof(RectTransform), typeof(ScrollRect));
        host.layer = target.gameObject.layer;
        var root = (RectTransform)host.transform;
        root.SetParent(original.parent, false);
        root.SetSiblingIndex(original.GetSiblingIndex());
        root.anchorMin = original.anchorMin; root.anchorMax = original.anchorMax;
        root.pivot = original.pivot; root.offsetMin = original.offsetMin; root.offsetMax = original.offsetMax;

        var view = new GameObject("DialogueViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        view.layer = host.layer;
        var viewRect = (RectTransform)view.transform;
        viewRect.SetParent(root, false);
        viewRect.anchorMin = Vector2.zero; viewRect.anchorMax = Vector2.one;
        viewRect.offsetMin = Vector2.zero; viewRect.offsetMax = new Vector2(-20f, 0f);
        view.GetComponent<Image>().color = Color.clear; // Receive wheel/drag even between glyphs.

        original.SetParent(viewRect, false);
        original.anchorMin = new Vector2(0f, 1f); original.anchorMax = Vector2.one;
        original.pivot = new Vector2(0.5f, 1f);
        original.anchoredPosition = Vector2.zero; original.sizeDelta = Vector2.zero;
        target.enableAutoSizing = false; target.overflowMode = TextOverflowModes.Overflow;
        target.alignment = TextAlignmentOptions.TopLeft; target.raycastTarget = false;

        var bar = new GameObject("DialogueScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        bar.layer = host.layer;
        var barRect = (RectTransform)bar.transform; barRect.SetParent(root, false);
        barRect.anchorMin = new Vector2(1f, 0f); barRect.anchorMax = Vector2.one;
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.anchoredPosition = Vector2.zero; barRect.sizeDelta = new Vector2(12f, 0f);
        bar.GetComponent<Image>().color = new Color(0.07f, 0.12f, 0.17f, 0.95f);
        var thumb = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        thumb.layer = host.layer;
        var thumbRect = (RectTransform)thumb.transform; thumbRect.SetParent(barRect, false);
        thumbRect.anchorMin = Vector2.zero; thumbRect.anchorMax = Vector2.one;
        thumbRect.offsetMin = thumbRect.offsetMax = Vector2.zero;
        thumb.GetComponent<Image>().color = new Color(0.36f, 0.69f, 0.75f);
        var scrollbar = bar.GetComponent<Scrollbar>();
        scrollbar.handleRect = thumbRect; scrollbar.targetGraphic = thumb.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var scroller = host.GetComponent<ScrollRect>();
        scroller.viewport = viewRect; scroller.content = original;
        scroller.horizontal = false; scroller.vertical = true; scroller.inertia = false;
        scroller.movementType = ScrollRect.MovementType.Clamped; scroller.scrollSensitivity = 40f;
        scroller.verticalScrollbar = scrollbar;
        scroller.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        var layout = host.AddComponent<DialogueTextScroll>();
        layout.text = target; layout.scroll = scroller; layout.viewport = viewRect;
    }

    private void OnEnable() { previousText = null; }
    private void LateUpdate() { RefreshLayout(); }

    public void ResetToTop()
    {
        // A synchronous local reply may replace the thinking text in the same
        // frame with an identical transcript. Text equality is not a new-page signal.
        previousText = null;
        if (scroll == null) return;
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
    }

    public void RefreshLayout()
    {
        if (text == null || viewport == null || scroll == null) return;
        Vector2 size = viewport.rect.size;
        if (size.x <= 0f || size.y <= 0f) return;
        bool changed = previousText != text.text;
        if (!changed && previousSize == size) return;
        float position = scroll.verticalNormalizedPosition;
        float height = Mathf.Max(size.y, text.GetPreferredValues(text.text, size.x, Mathf.Infinity).y + 6f);
        text.rectTransform.sizeDelta = new Vector2(0f, height);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = changed ? 1f : position;
        previousText = text.text; previousSize = size;
    }
}
