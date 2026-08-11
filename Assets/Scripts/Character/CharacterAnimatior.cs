using System.Collections;
using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    [Header("Components")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    // Animator Parameter 이름
    private static readonly int IsFront   = Animator.StringToHash("isFront");
    private static readonly int IsWalking = Animator.StringToHash("isWalking");
    private static readonly int IsWorking = Animator.StringToHash("isWorking");
    
    private float _targetSpeed = 0f; // 실제로 원하는 speed (IsRunning 무관)
    private float? _speedMultiplierOverride; // 잭팟 등 연출 중 전역 배속(OfficeManager.CharacterSpeedMultiplier) 무시하고 강제 지정. null이면 평소대로.

    void Update()
    {
        bool isRunning = GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunningForMovement;
        float multiplier = _speedMultiplierOverride
            ?? (OfficeManager.Instance != null ? OfficeManager.Instance.CharacterSpeedMultiplier : 1f);
        animator.speed = isRunning ? _targetSpeed * multiplier : 0f;
    }

    // 잭팟 연출 등 개별 캐릭터만 배속을 다르게 줘야 할 때 사용. null 전달 시 전역 배속으로 복귀.
    public void SetSpeedMultiplierOverride(float? multiplier) => _speedMultiplierOverride = multiplier;

    public bool GetCurrentIsFront()
    => animator.GetBool(IsFront);

    public void SetMoving(bool isMoving)
    {
        _targetSpeed = isMoving ? 1f : 0f;
        if (isMoving) animator.SetBool(IsWorking, false);
    }

    public void UpdateDirection(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.001f) return;

        _targetSpeed = 1f;
        bool isFront = delta.y <= 0f;
        bool flipX = delta.x < 0f;

        animator.SetBool(IsFront, isFront);
        spriteRenderer.flipX = flipX;
    }

    public void SetIdle(bool isWorking)
    {
        _targetSpeed = 0f;
        animator.SetBool(IsWorking, isWorking);
        animator.SetBool(IsFront, true);
        spriteRenderer.flipX = false;
        spriteRenderer.flipY = false;
    }

    public void SetWorking(bool isWorking)
    {
        _targetSpeed = isWorking ? 1f : 0f;
        animator.SetBool(IsWorking, isWorking);
        if (isWorking) animator.SetBool(IsFront, true);
        spriteRenderer.flipX = false;
        spriteRenderer.flipY = false;
    }

    public void SetFacing(bool isFront, bool flipX)
    {
        animator.SetBool(IsFront, isFront);
        spriteRenderer.flipX = flipX;
    }

    public void SetTalking()
    {
        StartCoroutine(TalkingFreeze());
    }

    IEnumerator TalkingFreeze()
    {
        var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        animator.Play(stateInfo.fullPathHash, 0, 0f); // 현재 상태 첫 프레임으로
        animator.speed = 1f;
        yield return null;
        _targetSpeed = 0f;
    }
}