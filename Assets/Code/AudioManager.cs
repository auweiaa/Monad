using System;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    [Header("AudioMixer")]
    [SerializeField] private AudioMixer mixer;

    private const float MinDb = -80f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (PlayerPrefs.HasKey("MasterVol")) 
        {
            float masterValue = PlayerPrefs.GetFloat("MasterVol");
            mixer.SetFloat("MasterVol", SliderToDb(masterValue));
        }

        if (PlayerPrefs.HasKey("MusicVol")) 
        {
            float musicValue = PlayerPrefs.GetFloat("MusicVol");
            mixer.SetFloat("MusicVol", SliderToDb(musicValue));
        }

        if (PlayerPrefs.HasKey("SFXVol")) 
        {
            float sfxValue = PlayerPrefs.GetFloat("SFXVol");
            mixer.SetFloat("SFXVol", SliderToDb(sfxValue));
        }
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
    public static float SliderToDb(float sliderValue)
    {
        if (sliderValue <= 0.0001f)
        {
            return MinDb;
        }
        return MathF.Log10(sliderValue) * 20f;
    }

    public static float DbToSlider(float db)
    {
        return Mathf.Pow(10f, db / 20f);
    }

}
