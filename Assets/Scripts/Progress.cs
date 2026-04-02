using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class Progress : MonoBehaviour
{
    [SerializeField] private Slider sliderProgress;
    [SerializeField] private TextMeshProUGUI textProgressData;
    [SerializeField] private float progressTime;

    private bool _loginComplete = false;
    private bool _allDataLoaded = false;

    public void SetLoginComplete() => _loginComplete = true;
    public void SetAllDataLoaded() => _allDataLoaded = true;

    public void Play(UnityAction action = null)
    {
        StartCoroutine(OnProgress(action));
    }

    private IEnumerator OnProgress(UnityAction action)
    {
        // Phase 1: 0% → 90%
        float elapsed = 0f;
        while (elapsed < progressTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / progressTime);
            SetSlider(Mathf.Lerp(0f, 0.9f, t));
            yield return null;
        }

        // Phase 2: 로그인 완료 대기
        SetSlider(0.9f);
        while (!_loginComplete)
            yield return null;

        // Phase 3: 모든 데이터 로드 완료 대기
        while (!_allDataLoaded)
            yield return null;

        // Phase 4: 90% → 100% (0.3초)
        elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.3f);
            SetSlider(Mathf.Lerp(0.9f, 1f, t));
            yield return null;
        }

        SetSlider(1f);
        yield return new WaitForSeconds(1f);
        action?.Invoke();
    }

    void SetSlider(float value)
    {
        sliderProgress.value = value;
        textProgressData.text = $"Now Loading...{value * 100:F0}%";
    }
}