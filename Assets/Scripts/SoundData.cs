using UnityEngine;

// 사운드 1개 = SO 1개 (A안).
// 클립 + 재생 메타데이터(볼륨/피치/루프)를 한 에셋에 묶어서 관리.
// 생성: Project 창 우클릭 → Create → Audio → Sound Data.
// 재생: SoundManager.Instance.Play(soundData);  (BGM/SFX 는 category 로 자동 분기)
public enum SoundCategory { BGM, SFX }

[CreateAssetMenu(menuName = "Audio/Sound Data", fileName = "Sound_")]
public class SoundData : ScriptableObject
{
    public SoundCategory category = SoundCategory.SFX;

    [Tooltip("재생할 오디오 클립")]
    public AudioClip clip;

    [Tooltip("이 사운드 고유 볼륨(설정창 마스터 볼륨과 곱해짐)")]
    [Range(0f, 1f)] public float volume = 1f;

    [Tooltip("효과음 다양화용 랜덤 피치 범위. (1,1)이면 변화 없음")]
    public Vector2 pitchRange = Vector2.one;

    [Tooltip("BGM 일 때 반복 재생 여부")]
    public bool loop = true;

    public float RandomPitch()
    {
        return pitchRange.x >= pitchRange.y
            ? pitchRange.x
            : Random.Range(pitchRange.x, pitchRange.y);
    }
}
