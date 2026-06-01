using UnityEngine;

// 전역 사운드 매니저 — 배경음(BGM) + 효과음(SFX).
// 볼륨은 PlayerPrefs 에 저장(0~1). 씬 전환에도 유지(DontDestroyOnLoad).
// 사용: SoundManager.Instance.PlayBGM(clip) / PlaySFX(clip), 볼륨은 설정창 슬라이더가 SetBgm/SetSfxVolume 호출.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("AudioSource (비우면 자동 생성)")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

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

        BgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_BGM, 1f));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_SFX, 1f));
        bgmSource.volume = BgmVolume;
        sfxSource.volume = SfxVolume;
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
