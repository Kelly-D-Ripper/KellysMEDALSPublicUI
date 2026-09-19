using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using KellysMedalsUi;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KellysMEDALSPublicUI;

// Uses this workspace's TALON/AIRLIFT MFD adapter and native game presentation.
// Only the display panel intercepts pointer input; map selection remains native.
internal sealed class MedalsMfdUi : IDisposable
{
    private const float Width = 460f, Height = 760f;
    private Color Green, Blue, Muted, Border, Dark;
    private readonly Plugin plugin;
    private readonly Action<string> log;
    private static readonly FieldInfo ScreensField = AccessTools.Field(typeof(VirtualMFD), "leftScreens");
    private static readonly FieldInfo ButtonsField = AccessTools.Field(typeof(VirtualMFD), "leftButtons");
    private static readonly FieldInfo ActiveField = AccessTools.Field(typeof(VirtualMFD), "activeLeft");
    private List<MFDScreen> screens = null!;
    private List<Button> buttons = null!;
    private MFDScreen? ActiveLeft
    {
        get => owner == null ? null : ActiveField.GetValue(owner) as MFDScreen;
        set { if (owner != null) ActiveField.SetValue(owner, value); }
    }

    private VirtualMFD? owner;
    private GameplayUI? gameplay;
    private MFDScreen? screen;
    private MfdSlotLease<MFDScreen>? lease;
    private RectTransform? root, panel;
    private Canvas? canvas, nativeCanvas;
    private GameObject? registration;
    private Button? bezel;
    private Button.ButtonClickedEvent? savedClick, ownClick;
    private string savedLabel = "";
    private bool savedEnabled, savedInteractable, wasVisible;
    private float nextBind, nextRefresh;
    private NativeMfdStyle? appearance;
    private string lastFailure = "";
    private readonly Vector3[] corners = new Vector3[4];

    internal MedalsMfdUi(Plugin plugin, Action<string> log) { this.plugin = plugin; this.log = log; }

    internal void Tick(bool enabled)
    {
        GameplayUI? current = SceneSingleton<GameplayUI>.i;
        if (!enabled || current != gameplay || owner == null || (lease != null && !lease.Owned))
        {
            Release();
            gameplay = current;
            if (!enabled) return;
        }
        if (screen == null && current != null && Time.unscaledTime >= nextBind)
        {
            nextBind = Time.unscaledTime + 2f;
            try { Bind(current); }
            catch (Exception error)
            {
                Release();
                string reason = error.GetType().Name + ": " + error.Message;
                if (lastFailure != reason) { log("MFD unavailable; MEDALS panel could not bind. " + reason); lastFailure = reason; }
            }
        }
        bool shown = screen != null && screen.isActive && DynamicMap.mapMaximized &&
            owner != null && owner.isActiveAndEnabled && ActiveLeft == screen && lease?.Owned == true;
        if (canvas != null && canvas.enabled != shown) canvas.enabled = shown;
        if (root != null && root.gameObject.activeSelf != shown) root.gameObject.SetActive(shown);
        if (!shown && screen != null && screen.isActive && owner != null && ActiveLeft != screen)
            screen.CloseScreen(Vector3.zero);
        if (shown)
        {
            // Native MFDScreen movement affects only registration, never the overlay canvas.
            Layout();
            if (!wasVisible || contentDirty || Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 1f;
                Refresh();
            }
        }
        wasVisible = shown;
    }

    private void Bind(GameplayUI current)
    {
        var mfds = current.GetComponentsInChildren<VirtualMFD>(true);
        if (mfds.Length != 1) return;
        if (ScreensField == null || ButtonsField == null || ActiveField == null)
            throw new MissingFieldException("Native MFD registration fields changed.");
        owner = mfds[0];
        screens = ScreensField.GetValue(owner) as List<MFDScreen> ?? throw new InvalidOperationException("Native MFD screen list unavailable.");
        buttons = ButtonsField.GetValue(owner) as List<Button> ?? throw new InvalidOperationException("Native MFD button list unavailable.");
        ReadVanillaPalette();
        appearance = new NativeMfdStyle(screens[1], Width);
        nativeCanvas = owner.GetComponentInParent<Canvas>();
        if (nativeCanvas == null) { owner = null; return; }
        // Separate native registration from rendering. Native Show/Close moves its transform;
        // inherited map canvases can also clip, scale or sort an otherwise active panel away.
        registration = Rect("MEDALS MFD registration", owner.transform).gameObject;
        screen = registration.AddComponent<MFDScreen>();
        var overlay = new GameObject("MEDALS MFD overlay", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        root = overlay.GetComponent<RectTransform>();
        canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = nativeCanvas.targetDisplay;
        int sortOrder = nativeCanvas.rootCanvas.sortingOrder;
        foreach (var existing in current.GetComponentsInChildren<Canvas>(true))
            sortOrder = Math.Max(sortOrder, existing.sortingOrder);
        canvas.sortingOrder = Math.Min(32767, sortOrder + 1);
        canvas.enabled = false;
        root.gameObject.SetActive(false);
        lease = MfdSlotLease<MFDScreen>.TryClaim(screens, buttons.Count, screen,
            index => buttons[index] != null &&
                buttons[index].GetComponentInChildren<TextMeshProUGUI>(true) != null);
        if (lease == null)
        {
            Release();
            if (lastFailure != "full") log("No unused left MFD button; MEDALS panel could not bind.");
            lastFailure = "full";
            return;
        }
        bezel = buttons[lease.Index];
        var label = bezel.GetComponentInChildren<TextMeshProUGUI>(true);
        savedLabel = label.text;
        savedEnabled = bezel.enabled;
        savedInteractable = bezel.interactable;
        savedClick = bezel.onClick;

        // A private highlight avoids changing the stock button's art or another screen's state.
        var highlight = Image("MEDALS selected", bezel.transform, Green, false);
        Stretch(highlight.rectTransform, 2f);
        var vanillaHighlight = screens.Count > 2 ? screens[2]?.highlight : null;
        if (vanillaHighlight != null)
        { highlight.color = vanillaHighlight.color; highlight.sprite = vanillaHighlight.sprite; highlight.type = vanillaHighlight.type; }
        highlight.enabled = false;
        screen.label = label;
        screen.highlight = highlight;
        screen.aircraftOnly = false;
        panel = Rect("MEDALS panel", root);
        panel.sizeDelta = new Vector2(Width, Height);
        panel.pivot = new Vector2(0, 1);
        panel.anchorMin = panel.anchorMax = new Vector2(0, 1);
        screen.displayPanel = panel.gameObject;
        ownClick = new Button.ButtonClickedEvent();
        ownClick.AddListener(Toggle);
        bezel.onClick = ownClick;
        screen.Setup(owner, "MED");
        Build();
        bezel.enabled = bezel.interactable = true;
        screen.CloseScreen(-Vector3.right * Screen.width);
        lastFailure = "";
        log("Registered MEDALS stats page in left MFD slot " + (lease.Index + 1) + ".");
    }

    private void Toggle()
    {
        if (owner == null || screen == null || lease?.Owned != true || !DynamicMap.mapMaximized) return;
        if (screen.isActive)
        {
            screen.CloseScreen(-Vector3.right * Screen.width);
            if (ActiveLeft == screen) ActiveLeft = null;
        }
        else
        {
            plugin.MfdOpened();
            owner.HideAllLeftScreens();
            ActiveLeft = screen;
            screen.ShowScreen(Vector3.zero);
            canvas!.enabled = true;
            root!.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Layout();
            Refresh();
            log("Panel opened: overlay=" + canvas.renderMode + " sort=" + canvas.sortingOrder +
                " screen=" + Screen.width + "x" + Screen.height + " panel=" + panel!.anchoredPosition +
                " scale=" + panel.localScale.x);
        }
    }

    internal void Close()
    {
        if (canvas != null) canvas.enabled = false;
        if (root != null) root.gameObject.SetActive(false);
        if (screen == null) return;
        screen.CloseScreen(-Vector3.right * Screen.width);
        if (owner != null && ActiveLeft == screen) ActiveLeft = null;
    }

    private void Layout()
    {
        if (root == null || panel == null || bezel == null) return;
        ((RectTransform)bezel.transform).GetWorldCorners(corners);
        Camera? camera = nativeCanvas != null && nativeCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? nativeCanvas.worldCamera : null;
        float leftEdge = RectTransformUtility.WorldToScreenPoint(camera, corners[0]).x;
        if (nativeCanvas != null) appearance?.Layout(panel, nativeCanvas, leftEdge, Width, Height);
        else
        {
            var placement = MfdPanelLayout.Calculate(Screen.width, Screen.height, leftEdge, Width, Height);
            panel.localScale = Vector3.one * placement.Scale;
            panel.anchoredPosition = new Vector2(placement.X, -placement.Y);
        }
    }

    internal bool Visible => screen != null && screen.isActive && owner != null && ActiveLeft == screen && DynamicMap.mapMaximized && lease?.Owned == true;
    private TMP_Text? summary, freshness, categoryLabel, modeLabel, pageLabel, detail;
    private ScrollRect? detailScroll;
    private string detailKey = "";
    private readonly List<Button> tabs = new List<Button>();
    private readonly List<Button> rowButtons = new List<Button>();
    private readonly List<TMP_Text> rowLabels = new List<TMP_Text>();
    private readonly List<Image> bars = new List<Image>();
    private Button? previousPage, nextPage;
    private string category = "ALL", mode = "ALL", selected = "";
    private int tab, page;
    private List<MedalView> visible = new List<MedalView>();
    private readonly MedalsViewCache view = new MedalsViewCache();
    private bool contentDirty = true;
    private int freshnessAge = int.MinValue;

    private void Build()
    {
        appearance!.Panel(panel!, Border, Dark);
        Text(panel!, "MEDALS", 18, 6, 424, 32, 30, Green, TextAlignmentOptions.Center);
        summary = Text(panel!, "YOUR CAREER RECORD", 18, 42, 424, 25, 17, Green, TextAlignmentOptions.Center);
        freshness = Text(panel!, "Waiting for server…", 18, 72, 424, 22, 12, Muted, TextAlignmentOptions.Center);
        string[] labels = { "NEXT AWARDS", "EARNED", "ALL MEDALS" };
        for (int i = 0; i < labels.Length; i++)
        {
            int target = i;
            tabs.Add(Button(panel!, labels[i], 18 + i * 143, 105, 138, 34, () => { tab = target; page = 0; selected = ""; }));
        }
        categoryLabel = Selector(panel!, 148, () => CycleCategory(-1), () => CycleCategory(1));
        modeLabel = Selector(panel!, 190, () => CycleMode(-1), () => CycleMode(1));
        for (int i = 0; i < 5; i++)
        {
            int slot = i;
            float y = 235 + i * 62;
            var button = Button(panel!, "", 18, y, 424, 56, () => {
                int index = page * 5 + slot;
                if (index < visible.Count) selected = visible[index].Key;
            });
            rowButtons.Add(button);
            var label = button.GetComponentInChildren<TMP_Text>();
            Place(label.rectTransform, 10, 3, 404, 44);
            label.fontSize = 14;
            label.alignment = TextAlignmentOptions.TopLeft;
            rowLabels.Add(label);
            var bar = Rect("Progress", button.transform).gameObject.AddComponent<Image>();
            bar.color = Green; bar.raycastTarget = false;
            Place(bar.rectTransform, 10, 49, 0, 3);
            bars.Add(bar);
        }
        previousPage = Button(panel!, "<", 18, 552, 42, 32, () => { page = Math.Max(0, page - 1); selected = ""; });
        pageLabel = Text(panel!, "", 65, 552, 330, 32, 13, Green, TextAlignmentOptions.Center);
        nextPage = Button(panel!, ">", 400, 552, 42, 32, () => { page++; selected = ""; });
        var detailBox = Image("Medal details (scroll for full requirements)", panel!, Border, true);
        Place(detailBox.rectTransform, 18, 592, 424, 116);
        var viewport = Rect("Details viewport", detailBox.transform);
        Place(viewport, 6, 6, 412, 104);
        viewport.gameObject.AddComponent<RectMask2D>();
        detail = Text(viewport, "Select a medal to see its requirements.", 0, 0, 412, 104, 14, Green, TextAlignmentOptions.TopLeft);
        detail.overflowMode = TextOverflowModes.Overflow;
        detailScroll = detailBox.gameObject.AddComponent<ScrollRect>();
        detailScroll.viewport = viewport; detailScroll.content = detail.rectTransform;
        detailScroll.horizontal = false; detailScroll.vertical = true;
        detailScroll.movementType = ScrollRect.MovementType.Clamped;
        detailScroll.scrollSensitivity = 20;
        Button(panel!, "CLOSE", 18, 718, 104, 28, Close);
        Text(panel!, "PERSONAL RECORD • SERVER VERIFIED", 132, 718, 310, 28, 11, Muted, TextAlignmentOptions.MidlineRight);
    }

    private void CycleCategory(int direction)
    {
        var categories = new[] { "ALL" }.Concat((plugin.Session.Rows ?? new List<MedalView>()).Select(r => r.Category).Distinct().OrderBy(c => c, StringComparer.Ordinal)).ToList();
        int index = Math.Max(0, categories.IndexOf(category));
        category = categories[(index + direction + categories.Count) % categories.Count];
        page = 0; selected = "";
    }
    private void CycleMode(int direction)
    {
        string[] modes = { "ALL", "PvP", "PvE", "Universal" };
        mode = modes[(Array.IndexOf(modes, mode) + direction + modes.Length) % modes.Length];
        page = 0; selected = "";
    }
    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    private void Refresh()
    {
        var rows = plugin.Session.Rows;
        int age = rows == null ? -2 : plugin.Session.Fresh(Time.unscaledTime) ? Math.Max(0, (int)(Time.unscaledTime - plugin.Session.ReceivedAt)) : -1;
        if (age != freshnessAge)
        {
            freshnessAge = age;
            freshness!.text = age == -2 ? "Awaiting MEDALS server 1.13.1+…" : age == -1 ? "STALE • waiting for server refresh" : "LIVE • updated " + age + "s ago";
        }
        bool changed = view.Update(rows, category, mode, tab);
        if (!contentDirty && !changed) return;
        contentDirty = false;
        categoryLabel!.text = "CATEGORY: " + category.ToUpperInvariant();
        modeLabel!.text = "TRACK: " + mode.ToUpperInvariant();
        for (int i = 0; i < tabs.Count; i++)
        {
            var colors = tabs[i].colors; colors.normalColor = i == tab ? Green : Border; tabs[i].colors = colors;
        }
        summary!.text = rows == null ? "YOUR CAREER RECORD" : view.Earned + " / " + rows.Count + " MEDALS EARNED";
        visible = view.Visible;
        int pages = Math.Max(1, (visible.Count + 4) / 5);
        page = Math.Max(0, Math.Min(page, pages - 1));
        previousPage!.interactable = page > 0;
        nextPage!.interactable = page < pages - 1;
        pageLabel!.text = visible.Count + " MATCHING  •  " + (page + 1) + " / " + pages;
        var selection = visible.FirstOrDefault(r => r.Key == selected);
        if (selection == null && visible.Count > page * 5) { selection = visible[page * 5]; selected = selection.Key; }
        for (int i = 0; i < rowButtons.Count; i++)
        {
            int index = page * 5 + i;
            bool exists = index < visible.Count;
            rowButtons[i].gameObject.SetActive(exists);
            if (!exists) continue;
            var row = visible[index];
            string state = row.Revoked ? "REVOKED" : row.Earned ? "EARNED" : Number(row.Progress) + " / " + Number(row.Threshold) + "  (" + (row.Fraction * 100).ToString("0", CultureInfo.InvariantCulture) + "%)";
            rowLabels[i].text = row.Name + "\n" + row.Tier.ToUpperInvariant() + " • " + state;
            rowLabels[i].color = row.Revoked ? Muted : Green;
            bars[i].rectTransform.sizeDelta = new Vector2((float)(row.Earned ? 1 : row.Revoked ? 0 : row.Fraction) * 404, 3);
            var colors = rowButtons[i].colors; colors.normalColor = row.Key == selected ? Green : Border; rowButtons[i].colors = colors;
        }
        detail!.text = selection == null ? rows == null ? "Your stats appear when this server supports MEDALS panels. No stats are stored on this client."
            : "No medals match this view. Try another category or track." : selection.Name + " • " + selection.Mode + "\n" + selection.Description +
            (selection.Revoked ? "\nRevoked by an administrator." : selection.Earned ? "\nEarned " + selection.AwardedUtc.Substring(0, Math.Min(10, selection.AwardedUtc.Length)) + " (UTC)" : "");
        detail.rectTransform.sizeDelta = new Vector2(412, Math.Max(104, detail.preferredHeight));
        if (detailKey != selected) { detailKey = selected; detailScroll!.verticalNormalizedPosition = 1; }
    }
    private void ReadVanillaPalette()
    {
        var textColors = new List<MfdPaletteColor>();
        var imageColors = new List<MfdPaletteColor>();
        // Read the stock BDF/MAP/HUD graphics after native styling has resolved inheritance.
        // Theme.GetColors returns override data: alpha zero means KEEP the original alpha,
        // and RGB zero means KEEP the original colour. It is not a render-ready palette.
        var stockScreens = screens;
        for (int i = 0; i < Math.Min(3, stockScreens.Count); i++)
        {
            var display = stockScreens[i]?.displayPanel;
            if (display == null) continue;
            foreach (var graphic in display.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.enabled) continue;
                var c = graphic.color;
                var sample = new MfdPaletteColor(c.r, c.g, c.b, c.a);
                if (graphic is TMP_Text) textColors.Add(sample);
                else if (graphic is Image) imageColors.Add(sample);
            }
        }
        Color Pick(List<MfdPaletteColor> colors, Color reference)
        {
            var c = MfdPaletteColor.Pick(colors,
                new MfdPaletteColor(reference.r, reference.g, reference.b, reference.a));
            return new Color(c.R, c.G, c.B, c.A);
        }
        Green = Pick(textColors, Color.green);
        Blue = Pick(textColors, new Color(0f, .4f, 1f));
        Muted = Pick(textColors, Color.gray);
        Border = Pick(imageColors, Color.gray);
        Dark = Pick(imageColors, new Color(0f, 0f, 0f, .6f));
        log("Resolved native palette: green=" + Green + " blue=" + Blue + " text=" + Muted +
            " border=" + Border + " background=" + Dark);
    }
    private TMP_Text Selector(Transform parent, float y, Action previous, Action next)
    {
        Button(parent, "<", 18, y, 34, 36, previous);
        var field = Image("Selection", parent, Border, true);
        Place(field.rectTransform, 58, y, 344, 36);
        var label = Text(field.transform, "", 8, 3, 328, 30, 15, Blue, TextAlignmentOptions.Center);
        Button(parent, ">", 408, y, 34, 36, next);
        return label;
    }

    private void Heading(Transform parent, string label, float y)
    {
        Text(parent, label, 18, y, 424, 20, 12, Green);
    }

    private Button Button(Transform parent, string label, float x, float y, float w, float h, Action click)
    {
        var border = Image(label, parent, Border, true);
        Place(border.rectTransform, x, y, w, h);
        var button = border.gameObject.AddComponent<Button>();
        button.targetGraphic = border;
        border.color = Color.white; // neutral multiplier; ColorBlock supplies exact vanilla colours
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors;
        colors.normalColor = Border;
        colors.highlightedColor = Green;
        colors.selectedColor = Green;
        colors.pressedColor = Green;
        colors.disabledColor = Muted;
        button.colors = colors;
        Text(border.transform, label, 5, 2, w - 10, h - 4, 13, Green, TextAlignmentOptions.Center);
        button.onClick.AddListener(() => { click(); contentDirty = true; nextRefresh = 0f; });
        return button;
    }

    private TMP_Text Text(Transform parent, string value, float x, float y, float w, float h,
                          float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        var rect = Rect("Text", parent);
        Place(rect, x, y, w, h);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        appearance!.TextStyle(text, size >= 27f);
        text.fontSize = size;
        text.text = value;
        text.color = color;
        text.alignment = align;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.richText = false;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private Image Image(string name, Transform parent, Color color, bool raycast)
    {
        var image = Rect(name, parent).gameObject.AddComponent<Image>();
        appearance?.Box(image);
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static void Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private void Release()
    {
        bool owned = lease?.Owned == true;
        bool vacant = owner != null && lease != null &&
            (lease.Index >= screens.Count || screens[lease.Index] == null);
        if (screen != null)
        {
            if (owner != null && ActiveLeft == screen) ActiveLeft = null;
            if (screen.displayPanel != null) screen.displayPanel.SetActive(false);
            if (screen.highlight != null) UnityEngine.Object.Destroy(screen.highlight.gameObject);
        }
        // Restore exactly the event object we replaced; keep a successor's listeners/label intact.
        if (bezel != null && ReferenceEquals(bezel.onClick, ownClick))
        {
            bezel.onClick = savedClick!;
            if (owned || vacant)
            {
                bezel.enabled = savedEnabled;
                bezel.interactable = savedInteractable;
                var label = bezel.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = savedLabel;
            }
        }
        lease?.Dispose();
        if (registration != null) UnityEngine.Object.Destroy(registration);
        registration = null; nativeCanvas = null; appearance = null;
        if (root != null) { root.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        lease = null; screen = null; root = panel = null; owner = null; bezel = null; canvas = null;
        savedClick = ownClick = null;
        tabs.Clear(); rowButtons.Clear(); rowLabels.Clear(); bars.Clear();
        detailScroll = null; detailKey = "";
        visible.Clear(); selected = ""; page = 0;
        view.Reset(); contentDirty = true; freshnessAge = int.MinValue;

        wasVisible = false;
    }

    public void Dispose() => Release();
}
