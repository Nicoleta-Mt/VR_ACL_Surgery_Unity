using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SurgicalChecklist — attach to a World Space Canvas next to the operating table.
///
/// Setup:
///   1. Create a World Space Canvas, size it (e.g. 0.6 x 0.8 m), face it toward the surgeon.
///   2. Attach this script to the Canvas root.
///   3. The script builds the full UI at runtime — no manual prefab wiring needed.
///   4. Add an AudioSource to the same GameObject and assign completionClip for the finish sound.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SurgicalChecklist : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [Header("Tasks — edit freely")]
    public List<string> tasks = new List<string>
    {
        "Patient positioned correctly",
        "Tourniquet applied & pressure set",
        "Surgical site cleaned & draped",
        "Arthroscope assembled",
        "Camera white-balanced",
        "Irrigation fluid connected",
        "Endoscope attached to portal",
        "Image confirmed on monitor",
        "Instruments counted & verified",
        "Recording started"
    };

    [Header("Audio")]
    [Tooltip("Clip played when all tasks are ticked. Leave empty to use a generated beep.")]
    public AudioClip completionClip;

    [Header("Colours")]
    public Color backgroundColour  = new Color(0.06f, 0.09f, 0.13f, 0.97f);
    public Color headerColour      = new Color(0.18f, 0.55f, 0.75f, 1f);
    public Color rowNormalColour   = new Color(0.10f, 0.14f, 0.20f, 1f);
    public Color rowCheckedColour  = new Color(0.07f, 0.22f, 0.18f, 1f);
    public Color checkmarkColour   = new Color(0.20f, 0.85f, 0.55f, 1f);
    public Color textColour        = new Color(0.88f, 0.92f, 0.96f, 1f);
    public Color textCheckedColour = new Color(0.40f, 0.80f, 0.60f, 1f);
    public Color completeBannerCol = new Color(0.10f, 0.70f, 0.45f, 1f);

    // ── private ────────────────────────────────────────────────────────────────

    private bool[]          _checked;
    private Image[]         _rowBg;
    private TextMeshProUGUI[] _labels;
    private Image[]         _checkIcons;
    private TextMeshProUGUI _progressText;
    private GameObject      _completeBanner;
    private AudioSource     _audio;
    private bool            _soundPlayed;

    // ── lifecycle ──────────────────────────────────────────────────────────────

    void Awake()
    {
        _audio   = GetComponent<AudioSource>();
        _checked = new bool[tasks.Count];
        BuildUI();
    }

    // ── UI construction ────────────────────────────────────────────────────────

    void BuildUI()
    {
        RectTransform canvasRect = GetComponent<RectTransform>();
        float W = canvasRect.rect.width;
        float H = canvasRect.rect.height;

        // ── background panel ──────────────────────────────────────────────────
        GameObject bg = CreateRect("Background", transform);
        SetFill(bg);
        bg.AddComponent<Image>().color = backgroundColour;

        // ── header bar ────────────────────────────────────────────────────────
        float headerH = H * 0.10f;
        GameObject header = CreateRect("Header", bg.transform);
        RectTransform hRect = header.GetComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0, 1); hRect.anchorMax = new Vector2(1, 1);
        hRect.offsetMin = new Vector2(0, -headerH); hRect.offsetMax = Vector2.zero;
        header.AddComponent<Image>().color = headerColour;

        TextMeshProUGUI title = CreateTMP("Title", header.transform);
        SetFill(title.gameObject);
        title.text      = "☰  PRE-OP CHECKLIST";
        title.fontSize  = 18;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform tRect = title.GetComponent<RectTransform>();
        tRect.offsetMin = new Vector2(20, 0); tRect.offsetMax = new Vector2(-20, 0);
        title.color = Color.white;

        // ── progress label ────────────────────────────────────────────────────
        float progressH = H * 0.06f;
        GameObject progRow = CreateRect("ProgressRow", bg.transform);
        RectTransform prRect = progRow.GetComponent<RectTransform>();
        prRect.anchorMin = new Vector2(0, 1); prRect.anchorMax = new Vector2(1, 1);
        prRect.offsetMin = new Vector2(0, -(headerH + progressH));
        prRect.offsetMax = new Vector2(0, -headerH);
        progRow.AddComponent<Image>().color = new Color(0.04f, 0.07f, 0.11f, 1f);

        _progressText = CreateTMP("Progress", progRow.transform);
        SetFill(_progressText.gameObject);
        _progressText.fontSize  = 13;
        _progressText.alignment = TextAlignmentOptions.MidlineLeft;
        _progressText.color     = new Color(0.55f, 0.70f, 0.85f, 1f);
        RectTransform prTRect = _progressText.GetComponent<RectTransform>();
        prTRect.offsetMin = new Vector2(20, 0); prTRect.offsetMax = new Vector2(-20, 0);

        // ── task rows ─────────────────────────────────────────────────────────
        float topOffset   = headerH + progressH;
        float bottomPad   = H * 0.10f;
        float availableH  = H - topOffset - bottomPad;
        float rowH        = availableH / tasks.Count;

        _rowBg      = new Image[tasks.Count];
        _labels     = new TextMeshProUGUI[tasks.Count];
        _checkIcons = new Image[tasks.Count];

        for (int i = 0; i < tasks.Count; i++)
        {
            int idx = i; // capture for lambda

            // row background
            GameObject row = CreateRect("Row_" + i, bg.transform);
            RectTransform rRect = row.GetComponent<RectTransform>();
            rRect.anchorMin = new Vector2(0, 1); rRect.anchorMax = new Vector2(1, 1);
            rRect.offsetMin = new Vector2(4,  -(topOffset + rowH * (i + 1)) + 2);
            rRect.offsetMax = new Vector2(-4, -(topOffset + rowH * i)       - 2);

            _rowBg[i]       = row.AddComponent<Image>();
            _rowBg[i].color = rowNormalColour;

            // add button
            Button btn = row.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.18f, 0.26f, 0.36f, 1f);
            cb.pressedColor     = new Color(0.10f, 0.18f, 0.25f, 1f);
            btn.colors          = cb;
            btn.onClick.AddListener(() => Toggle(idx));

            // checkbox box
            GameObject box = CreateRect("Box", row.transform);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0, 0.5f); boxRect.anchorMax = new Vector2(0, 0.5f);
            boxRect.pivot     = new Vector2(0, 0.5f);
            float boxSize     = Mathf.Min(rowH * 0.55f, 22f);
            boxRect.sizeDelta = new Vector2(boxSize, boxSize);
            boxRect.anchoredPosition = new Vector2(16, 0);
            Image boxImg  = box.AddComponent<Image>();
            boxImg.color  = new Color(0.15f, 0.22f, 0.32f, 1f);
            // border via outline
            Outline outline = box.AddComponent<Outline>();
            outline.effectColor    = new Color(0.30f, 0.50f, 0.70f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // checkmark (✔) inside box
            TextMeshProUGUI check = CreateTMP("Check", box.transform);
            SetFill(check.gameObject);
            check.text      = "✔";
            check.fontSize  = boxSize * 0.65f;
            check.alignment = TextAlignmentOptions.Midline;
            check.color     = checkmarkColour;
            _checkIcons[i]  = check.GetComponent<Image>(); // store Image on same GO for hide/show
            // We'll toggle the TMP component directly
            check.gameObject.SetActive(false);
            // store TMP reference via tag trick — easier: just keep a TMP array
            // Overwrite _checkIcons[i] strategy: store the TMP instead
            // (reuse Image array slot with a small workaround)
            _checkIcons[i] = null; // will use separate array below

            // keep TMP ref — re-assign properly
            if (i == 0) // initialise arrays on first pass
            {
                // Already declared above, just proceed
            }
            // Store check TMP in a helper method
            StoreCheckRef(i, check);

            // task label
            TextMeshProUGUI label = CreateTMP("Label_" + i, row.transform);
            RectTransform lRect  = label.GetComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0, 0); lRect.anchorMax = new Vector2(1, 1);
            lRect.offsetMin = new Vector2(boxSize + 28, 0);
            lRect.offsetMax = new Vector2(-12, 0);
            label.text      = tasks[i];
            label.fontSize  = Mathf.Clamp(rowH * 0.38f, 11f, 16f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color     = textColour;
            _labels[i]      = label;

            // divider line
            if (i < tasks.Count - 1)
            {
                GameObject div = CreateRect("Divider_" + i, row.transform);
                RectTransform dRect = div.GetComponent<RectTransform>();
                dRect.anchorMin = new Vector2(0, 0); dRect.anchorMax = new Vector2(1, 0);
                dRect.offsetMin = new Vector2(12, -1); dRect.offsetMax = new Vector2(-12, 0);
                div.AddComponent<Image>().color = new Color(1,1,1,0.05f);
            }
        }

        // ── completion banner ─────────────────────────────────────────────────
        _completeBanner = CreateRect("CompleteBanner", bg.transform);
        RectTransform compRect = _completeBanner.GetComponent<RectTransform>();
        compRect.anchorMin = new Vector2(0, 0); compRect.anchorMax = new Vector2(1, 0);
        compRect.offsetMin = new Vector2(0, 0); compRect.offsetMax = new Vector2(0, bottomPad);
        _completeBanner.AddComponent<Image>().color = completeBannerCol;

        TextMeshProUGUI compLabel = CreateTMP("CompleteLabel", _completeBanner.transform);
        SetFill(compLabel.gameObject);
        compLabel.text      = "✔  ALL TASKS COMPLETE";
        compLabel.fontSize  = 15;
        compLabel.fontStyle = FontStyles.Bold;
        compLabel.alignment = TextAlignmentOptions.Midline;
        compLabel.color     = Color.white;
        _completeBanner.SetActive(false);

        UpdateProgress();
    }

    // ── check TMP refs (separate array to avoid Image/TMP confusion) ───────────
    private TextMeshProUGUI[] _checkTMPs;

    void StoreCheckRef(int i, TextMeshProUGUI tmp)
    {
        if (_checkTMPs == null)
            _checkTMPs = new TextMeshProUGUI[tasks.Count];
        _checkTMPs[i] = tmp;
    }

    // ── toggle logic ───────────────────────────────────────────────────────────

    void Toggle(int idx)
    {
        _checked[idx] = !_checked[idx];

        // Row background colour
        _rowBg[idx].color = _checked[idx] ? rowCheckedColour : rowNormalColour;

        // Checkmark visibility
        if (_checkTMPs != null && _checkTMPs[idx] != null)
            _checkTMPs[idx].gameObject.SetActive(_checked[idx]);

        // Label colour
        _labels[idx].color = _checked[idx] ? textCheckedColour : textColour;

        UpdateProgress();
        CheckCompletion();
    }

    void UpdateProgress()
    {
        int done = 0;
        foreach (bool b in _checked) if (b) done++;
        if (_progressText != null)
            _progressText.text = $"  {done} / {tasks.Count}  tasks completed";
    }

    void CheckCompletion()
    {
        bool allDone = true;
        foreach (bool b in _checked) if (!b) { allDone = false; break; }

        if (_completeBanner != null)
            _completeBanner.SetActive(allDone);

        if (allDone && !_soundPlayed)
        {
            _soundPlayed = true;
            PlayCompletionSound();
        }
        else if (!allDone)
        {
            _soundPlayed = false;
        }
    }

    void PlayCompletionSound()
    {
        if (completionClip != null)
        {
            _audio.PlayOneShot(completionClip);
        }
        else
        {
            // Generated double-beep if no clip assigned
            StartCoroutine(GeneratedBeep());
        }
    }

    IEnumerator GeneratedBeep()
    {
        PlayBeep(880f, 0.12f);
        yield return new WaitForSeconds(0.18f);
        PlayBeep(1046f, 0.18f);
    }

    void PlayBeep(float frequency, float duration)
    {
        int sampleRate = AudioSettings.outputSampleRate;
        int samples    = Mathf.RoundToInt(sampleRate * duration);
        float[] data   = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t     = (float)i / sampleRate;
            float fade  = Mathf.Clamp01(1f - t / duration * 2f); // fade out
            data[i]     = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.4f * fade;
        }
        AudioClip clip = AudioClip.Create("beep", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        _audio.PlayOneShot(clip);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    static GameObject CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void SetFill(GameObject go)
    {
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI CreateTMP(string name, Transform parent)
    {
        GameObject go  = CreateRect(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.enableWordWrapping = false;
        tmp.overflowMode       = TextOverflowModes.Ellipsis;
        return tmp;
    }
}
