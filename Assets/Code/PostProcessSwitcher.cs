using UnityEngine;
using UnityEngine.Rendering;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PostProcessSwitcher : MonoBehaviour
{
    public enum Preset { Standard = 0, Assist1 = 1, Assist2 = 2 }

    private const string PrefKey = "PP_Preset";

    [Header("Assign the Volume components")]
    [SerializeField] private Volume standard; // PP_ColorCorrection
    [SerializeField] private Volume assist1; // PP_ColorBlindAssist1
    [SerializeField] private Volume assist2; // PP_ColorBlindAssist2

    [Header("Controls")]
    [SerializeField] private bool enableHotkey = true;
    [SerializeField] private KeyCode hotkey = KeyCode.O;

    void Start()
    {
        // failsafeing a "default" to Standard
        var preset = (Preset)PlayerPrefs.GetInt(PrefKey, (int)Preset.Standard);
        Apply(preset);
    }

    void Update()
    {
        if (!enableHotkey) return;

        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame) pressed = true; // Input System
#else
        if (Input.GetKeyDown(hotkey)) pressed = true; // Old input, did not work otherwise for some reason, maybe due to the new input system package being present?
#endif

        if (pressed) NextPreset();
    }

    public void NextPreset()
    {
        var current = (Preset)PlayerPrefs.GetInt(PrefKey, (int)Preset.Standard);
        var next = (Preset)(((int)current + 1) % 3);
        Apply(next);
    }

    // UI things can call these directly, or use SetPreset with an index
    public void SetStandard() => Apply(Preset.Standard);
    public void SetAssist1()  => Apply(Preset.Assist1);
    public void SetAssist2()  => Apply(Preset.Assist2);
    public void SetPreset(int presetIndex) => Apply((Preset)Mathf.Clamp(presetIndex, 0, 2));

    private void Apply(Preset preset)
    {
        PlayerPrefs.SetInt(PrefKey, (int)preset);
        PlayerPrefs.Save();

        // failsafe: one active at a time
        SetVolume(standard, preset == Preset.Standard);
        SetVolume(assist1,  preset == Preset.Assist1);
        SetVolume(assist2,  preset == Preset.Assist2);
    }

    private static void SetVolume(Volume v, bool on)
    {
        if (!v) return;
        v.enabled = true;
        v.weight = on ? 1f : 0f; // toggle contribution
    }
}
