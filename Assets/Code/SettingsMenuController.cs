using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class SettingsMenuController : MonoBehaviour
{
    [Header("Toggles")]
    [SerializeField] private Toggle fullScreenToggle;
    [SerializeField] private Toggle vsyncToggle;

    [Header("MenuManager")]
    [SerializeField] private MenuManager menuManager;

    [Header("Resolution")]
    [SerializeField] private TMP_Text resolutionLabel;

    private List<ResolutionItem> resolutions;
    private int selectedResolutionIndex;

    [Header("AudioMixer")]
    [SerializeField] private AudioMixer mixer;

    [Header("SoundLabels")]
    [SerializeField] private TMP_Text masterLabel;
    [SerializeField] private TMP_Text musicLabel;
    [SerializeField] private TMP_Text sfxLabel;

    [Header("Slider")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    void Start()
    {
        SetToggles();

        SetSoundSlides();

        resolutions = BuildResolutionList(); //guarantees a list due to a implemented fallback

        selectedResolutionIndex = SelectCurrentResolution(Screen.width, Screen.height);

        // if current resolution is not in the list -> update list
        if (selectedResolutionIndex == -1)
        {
            selectedResolutionIndex = InsertCurrentResolutionIntoResolutionList(Screen.width, Screen.height);
        }

        UpdateResolutionLabel();
    }

    private void SetToggles()
    {
        fullScreenToggle.isOn = Screen.fullScreen;

        if (QualitySettings.vSyncCount == 0)
        {
            vsyncToggle.isOn = false;
        }
        else
        {
            vsyncToggle.isOn = true;
        }
    }

    private List<ResolutionItem> BuildResolutionList()
    {
        // get all system-wide resolutions
        Resolution[] systemResolutions = Screen.resolutions;

        List<ResolutionItem> list = systemResolutions
            .Select(r => new ResolutionItem(r.width, r.height))
            .Distinct()
            .OrderBy(res => res.Horizontal)
            .ThenBy(res => res.Vertical)
            .ToList();

        // fallback, if reading the system resolutions failed
        if (list.Count == 0)
        {
            // insert new items in ascending order! so no sort is required (faster)
            list.Add(new ResolutionItem(854, 480));
            list.Add(new ResolutionItem(1280, 720));
            list.Add(new ResolutionItem(1920, 1080));
            list.Add(new ResolutionItem(2560, 1440));
            list.Add(new ResolutionItem(3840, 2160));
        }

        return list;
    }

    public void SwitchResolutionLeft()
    {
        selectedResolutionIndex = Mathf.Max(0, selectedResolutionIndex - 1);
        UpdateResolutionLabel();
    }


    private int SelectCurrentResolution(int currentWidth, int currentHeight)
    {
        for (int i = 0; i < resolutions.Count; i++)
        {
            if (currentWidth == resolutions[i].Horizontal && currentHeight == resolutions[i].Vertical)
            {
                return i;
            }
        }

        return -1;
    }


    // Inserts a new ResolutionItem to the List and Returns the Index 
    private int InsertCurrentResolutionIntoResolutionList(int currentWidth, int currentHeight)
    {
        ResolutionItem newItem = new ResolutionItem(currentWidth, currentHeight);

        for (int i = 0; i < resolutions.Count; i++)
        {
            if (newItem.Horizontal < resolutions[i].Horizontal)
            {
                resolutions.Insert(i, newItem);
                return i;
            }

            if (newItem.Horizontal == resolutions[i].Horizontal && newItem.Vertical <= resolutions[i].Vertical)
            {
                resolutions.Insert(i, newItem);
                return i;
            }
        }

        // newItem is the greatest
        resolutions.Add(newItem);
        return resolutions.Count - 1;
    }

    public void SwitchResolutionRight()
    {
        selectedResolutionIndex = Mathf.Min(resolutions.Count - 1, selectedResolutionIndex + 1);
        UpdateResolutionLabel();
    }

    private void UpdateResolutionLabel()
    {
        resolutionLabel.text = resolutions[selectedResolutionIndex].Horizontal + " x " + resolutions[selectedResolutionIndex].Vertical;
    }

    public void ApplyGraphics()
    {
        if (vsyncToggle.isOn)
        {
            QualitySettings.vSyncCount = 1;
        }
        else
        {
            QualitySettings.vSyncCount = 0;
        }

        Screen.SetResolution(resolutions[selectedResolutionIndex].Horizontal, resolutions[selectedResolutionIndex].Vertical, fullScreenToggle.isOn);
    }

    public void SetMasterVol()
    {
        float db = AudioManager.SliderToDb(masterSlider.value);

        mixer.SetFloat("MasterVol", db);

        masterLabel.text = Mathf.RoundToInt(masterSlider.value * 100f).ToString();

        PlayerPrefs.SetFloat("MasterVol", masterSlider.value);
    }

    public void SetMusicVol()
    {
        float db = AudioManager.SliderToDb(musicSlider.value);

        mixer.SetFloat("MusicVol", db);

        musicLabel.text = Mathf.RoundToInt(musicSlider.value * 100f).ToString();

        PlayerPrefs.SetFloat("MusicVol", musicSlider.value);
    }

    public void SetSFXVol()
    {
        float db = AudioManager.SliderToDb(sfxSlider.value);

        mixer.SetFloat("SFXVol", db);

        sfxLabel.text = Mathf.RoundToInt(sfxSlider.value * 100f).ToString();

        PlayerPrefs.SetFloat("SFXVol", sfxSlider.value);
    }
    public void OpenMainMenu()
    {
        PlayerPrefs.Save();
        menuManager.ShowMainMenu();
        Debug.Log("Open Main Menu");
    }

    // PlayerPrefs are set during start() in AudioManager
    private void SetSoundSlides()
    {
        float masterDb;
        if (mixer.GetFloat("MasterVol", out masterDb))
        {
            masterSlider.value = AudioManager.DbToSlider(masterDb);
            masterLabel.text = Mathf.RoundToInt(masterSlider.value * 100f).ToString();
        }

        float musicDb;
        if (mixer.GetFloat("MusicVol", out musicDb))
        {
            musicSlider.value = AudioManager.DbToSlider(musicDb);
            musicLabel.text = Mathf.RoundToInt(musicSlider.value * 100f).ToString();
        }

        float sfxDb;
        if (mixer.GetFloat("SFXVol", out sfxDb))
        {
            sfxSlider.value = AudioManager.DbToSlider(sfxDb);
            sfxLabel.text = Mathf.RoundToInt(sfxSlider.value * 100f).ToString();
        }
    }
}


public sealed class ResolutionItem : IEquatable<ResolutionItem>
{
    public int Horizontal { get; }
    public int Vertical { get; }

    public ResolutionItem(int horizontal, int vertical)
    {
        Horizontal = horizontal;
        Vertical = vertical;
    }

    public bool Equals(ResolutionItem other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Horizontal == other.Horizontal &&
               Vertical == other.Vertical;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as ResolutionItem);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Horizontal, Vertical);
    }
}
