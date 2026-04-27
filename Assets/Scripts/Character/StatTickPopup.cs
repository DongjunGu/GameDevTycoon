using System.Collections;
using TMPro;
using UnityEngine;

// 상시 개발틱 팝업: [statIcon] +N
// - 위로 안 떠오르고 제자리에서 카운트업 (총 totalCountDuration 초)
// - 잭팟이면 jackpotParticle 재생
// - 게임 시간 정지 중이면 일시정지
// - 카운트 종료 시 onFinish 콜백 (큐잉 매니저가 다음 팝업 띄움)
// - ActiveCount는 외부(OfficeCharacter)에서 관리 — 큐 대기까지 포함
public class StatTickPopup : MonoBehaviour
{
    public static int ActiveCount;   // queued + showing 합계 (OfficeCharacter에서 ++/--)

    [Header("자식 컴포넌트")]
    public SpriteRenderer  statIcon;
    public TextMeshPro     numberLabel;
    public ParticleSystem  jackpotParticle;

    [Header("타이밍")]
    public float totalCountDuration = 2f;
    public float holdAfter          = 0.3f;
    public float blankDuration      = 1f;

    private System.Action _onFinish;

    public void Show(Sprite stat, int target, Color color, bool playParticle, System.Action onFinish)
    {
        if (statIcon != null)
        {
            statIcon.sprite  = stat;
            statIcon.enabled = stat != null;
        }
        if (numberLabel != null)
        {
            numberLabel.color   = Color.white;   // 숫자는 항상 흰색
            numberLabel.text    = "";
            numberLabel.enabled = target > 0;
        }
        if (jackpotParticle != null)
        {
            jackpotParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (playParticle) jackpotParticle.Play();
        }

        _onFinish = onFinish;

        StopAllCoroutines();
        StartCoroutine(Animate(target));
    }

    IEnumerator Animate(int target)
    {
        if (target <= 0)
        {
            yield return WaitPaused(blankDuration);
            Finish();
            yield break;
        }

        // 처음부터 +1 표시 (아이콘이 떴다는 건 최소 1은 발동됐다는 뜻)
        if (numberLabel != null) numberLabel.text = "+1";

        if (target == 1)
        {
            // +1만 들어왔으면 totalCountDuration 동안 그대로 머무르다가 사라짐
            yield return WaitPaused(totalCountDuration);
        }
        else
        {
            // +1 → +target 까지 (target-1)번 전환, 총 totalCountDuration
            float interval = totalCountDuration / (target - 1);
            for (int i = 2; i <= target; i++)
            {
                yield return WaitPaused(interval);
                if (numberLabel != null) numberLabel.text = $"+{i}";
            }
            yield return WaitPaused(holdAfter);
        }

        Finish();
    }

    void Finish()
    {
        if (jackpotParticle != null)
            jackpotParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var cb = _onFinish;
        _onFinish = null;
        StatTickPopupPool.Instance?.Return(this);
        cb?.Invoke();
    }

    void OnDisable()
    {
        // 강제 비활성화 시 콜백 누락 방지
        if (_onFinish != null)
        {
            var cb = _onFinish;
            _onFinish = null;
            cb?.Invoke();
        }
    }

    IEnumerator WaitPaused(float seconds)
    {
        float remain = seconds;
        while (remain > 0f)
        {
            if (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunning)
                remain -= Time.deltaTime;
            yield return null;
        }
    }
}
