using System.Collections.Generic;
using UnityEngine;

// 전역 사운드 매니저 — 배경음(BGM) + 효과음(SFX).
// 볼륨은 PlayerPrefs 에 저장(0~1). 씬 전환에도 유지(DontDestroyOnLoad).
// 사용:
//   - SoundData(SO) 권장:  SoundManager.Instance.Play(soundData);  (category 로 BGM/SFX 자동 분기)
//   - AudioClip 직접:       SoundManager.Instance.PlayBGM(clip) / PlaySFX(clip)
// 볼륨은 설정창 슬라이더가 SetBgm/SetSfxVolume 호출.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("AudioSource (비우면 자동 생성)")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("SFX 보이스 풀 (랜덤 피치/볼륨 동시재생용)")]
    [SerializeField] private int sfxVoiceCount = 8;
    private readonly List<AudioSource> sfxVoices = new List<AudioSource>();

    [Header("시작 시 자동 재생할 BGM (비우면 안 함)")]
    [SerializeField] private SoundData defaultBgm;

    const string KEY_BGM = "sound_bgm_volume";
    const string KEY_SFX = "sound_sfx_volume";

    public float BgmVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        for (int i = 0; i < sfxVoiceCount; i++)
        {
            var v = gameObject.AddComponent<AudioSource>();
            v.playOnAwake = false;
            sfxVoices.Add(v);
        }

        BgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_BGM, 1f));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_SFX, 1f));
        bgmSource.volume = BgmVolume;
        sfxSource.volume = SfxVolume;
    }

    void Start()
    {
        // LoadingScene 에 배치되면 DontDestroyOnLoad 로 끝까지 유지되어 전체 씬에서 같은 BGM 이 흐름.
        if (defaultBgm != null) PlayBGM(defaultBgm);
    }

    // ── 배경음 ───────────────────────────────
    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (bgmSource == null || clip == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return; // 같은 곡이면 유지
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = BgmVolume;
        bgmSource.Play();
    }

    public void StopBGM()   { if (bgmSource != null) bgmSource.Stop(); }
    public void PauseBGM()  { if (bgmSource != null) bgmSource.Pause(); }
    public void ResumeBGM() { if (bgmSource != null) bgmSource.UnPause(); }

    // ── 효과음 ───────────────────────────────
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale)); // 실제 = sfxSource.volume * volumeScale
    }

    // ── SoundData(SO) 재생 ───────────────────
    // category 로 BGM/SFX 자동 분기. SFX 는 보이스 풀에서 빈 소스를 잡아 피치/볼륨을 개별 적용.
    public void Play(SoundData data, float volumeScale = 1f)
    {
        if (data == null || data.clip == null) return;
        if (data.category == SoundCategory.BGM) PlayBGM(data);
        else PlaySFX(data, volumeScale);
    }

    public void PlayBGM(SoundData data)
    {
        if (data == null) return;
        PlayBGM(data.clip, data.loop);
        if (bgmSource != null) bgmSource.volume = BgmVolume * data.volume;
    }

    public void PlaySFX(SoundData data, float volumeScale = 1f)
    {
        if (data == null || data.clip == null) return;
        var voice = GetFreeVoice();
        voice.clip = data.clip;
        voice.volume = SfxVolume * data.volume * Mathf.Clamp01(volumeScale);
        voice.pitch = data.RandomPitch();
        voice.loop = false;
        voice.Play();
    }

    // 재생 중이 아닌 보이스를 찾고, 모두 사용 중이면 새로 추가.
    private AudioSource GetFreeVoice()
    {
        for (int i = 0; i < sfxVoices.Count; i++)
            if (!sfxVoices[i].isPlaying) return sfxVoices[i];

        var v = gameObject.AddComponent<AudioSource>();
        v.playOnAwake = false;
        sfxVoices.Add(v);
        return v;
    }

    // ── 볼륨 (설정창 슬라이더 0~1) ─────────────
    public void SetBgmVolume(float v)
    {
        BgmVolume = Mathf.Clamp01(v);
        if (bgmSource != null) bgmSource.volume = BgmVolume;
        PlayerPrefs.SetFloat(KEY_BGM, BgmVolume);
        PlayerPrefs.Save();
    }

    public void SetSfxVolume(float v)
    {
        SfxVolume = Mathf.Clamp01(v);
        if (sfxSource != null) sfxSource.volume = SfxVolume;
        PlayerPrefs.SetFloat(KEY_SFX, SfxVolume);
        PlayerPrefs.Save();
    }
}
