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

    // 로그인 완료 시 GPGSLogin에서 호출
    public void SetLoginComplete()
    {
        _loginComplete = true;
    }

    public void Play(UnityAction action = null)
    {
        StartCoroutine(OnProgress(action));
    }

    private IEnumerator OnProgress(UnityAction action)
    {
        float current = 0;
        float percent = 0;

        // 로그인 완료 전까지 90%에서 멈춤
        while (percent < 1)
        {
            current += Time.deltaTime;
            percent = current / progressTime;

            // 로그인 완료 전엔 90%까지만
            float maxPercent = _loginComplete ? 1f : 0.9f;
            float clampedPercent = Mathf.Min(percent, maxPercent);

            // 90%에서 멈춘 상태로 로그인 완료 대기
            if (!_loginComplete && percent >= 0.9f)
            {
                clampedPercent = 0.9f;
                current = progressTime * 0.9f; // percent가 더 올라가지 않도록
            }

            sliderProgress.value = Mathf.Lerp(0, 1, clampedPercent);
            textProgressData.text = $"Now Loading...{sliderProgress.value * 100:F0}%";

            yield return null;
        }

        action?.Invoke();
    }
}