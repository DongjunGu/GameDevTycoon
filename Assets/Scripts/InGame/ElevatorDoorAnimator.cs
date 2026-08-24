using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 직원이 엘리베이터 셀에 도착해 워프를 타는 순간 문 열림 연출을 1회 재생한다.
///
/// 문 스프라이트(doorObject)는 평소 꺼둔 채로 두고, 트리거될 때만 켜서 Animator를 돌린 뒤 다시 끈다.
/// 끄는 건 Start에서 하는데, 이 컴포넌트는 문의 부모에 붙고 doorObject는 자식이라 자기 자신을 끄는
/// 사고(첫 재생이 영영 안 도는)가 나지 않는다.
///
/// CharacterMover가 워프 직전에 Notify(fromCell)을 호출하고, triggerCells에 그 셀이 들어있는 문만 반응한다.
/// 층마다 문을 따로 두고 각자 자기 셀만 등록하면 그대로 확장된다.
/// </summary>
public class ElevatorDoorAnimator : MonoBehaviour
{
    [Header("문 오브젝트")]
    [Tooltip("연출 중에만 켜지는 문 스프라이트. 비워두면 이 오브젝트 자신을 쓴다")]
    public GameObject doorObject;
    [Tooltip("doorObject의 Animator. 비워두면 doorObject에서 자동으로 찾는다")]
    public Animator animator;

    [Header("트리거")]
    [Tooltip("이 셀들에서 엘리베이터 워프가 시작될 때 문이 열린다 (예: 아래층 엘리베이터 셀)")]
    public Vector3Int[] triggerCells;

    [Header("타이밍")]
    [Tooltip("애니메이션 길이를 Animator에서 못 읽었을 때 쓸 폴백 시간(초). Elevator 클립 기준 0.75")]
    public float fallbackDuration = 0.75f;
    [Tooltip("재생할 Animator 상태 이름. 비워두면 컨트롤러 기본 상태를 처음부터 재생")]
    public string stateName = "";

    static readonly List<ElevatorDoorAnimator> _all = new();

    Coroutine _playing;

    void Awake()
    {
        if (doorObject == null) doorObject = gameObject;
        if (animator == null)   animator   = doorObject.GetComponent<Animator>();
        _all.Add(this);
    }

    void OnDestroy() => _all.Remove(this);

    void Start()
    {
        // 평소엔 닫혀 있는(= 문 스프라이트가 안 보이는) 상태가 기본.
        if (doorObject != null && doorObject != gameObject) doorObject.SetActive(false);
        if (animator != null) animator.enabled = false;
    }

    /// <summary>워프가 시작된 셀을 알린다 — 해당 셀을 담당하는 문이 있으면 열림 연출 1회 재생.</summary>
    public static void Notify(Vector3Int cell)
    {
        foreach (var door in _all)
        {
            if (door == null || !door.isActiveAndEnabled) continue;
            if (door.Handles(cell)) door.Play();
        }
    }

    bool Handles(Vector3Int cell)
    {
        if (triggerCells == null) return false;
        foreach (var c in triggerCells)
            if (c == cell) return true;
        return false;
    }

    public void Play()
    {
        if (doorObject == null) return;
        // 연속 호출되면 처음부터 다시 — 두 명이 잇따라 타도 문이 어정쩡하게 멈춰 있지 않게.
        if (_playing != null) StopCoroutine(_playing);
        _playing = StartCoroutine(CoPlay());
    }

    IEnumerator CoPlay()
    {
        doorObject.SetActive(true);

        if (animator == null)
        {
            yield return new WaitForSeconds(fallbackDuration);
        }
        else
        {
            animator.enabled = true;
            // Rebind로 기본 상태 처음 프레임으로 되돌린다 — 이전 재생이 끝난 자리에서 이어지지 않게.
            // stateName을 지정하면 그 상태를 명시적으로 재생한다(컨트롤러에 상태가 여러 개일 때).
            animator.Rebind();
            if (!string.IsNullOrEmpty(stateName)) animator.Play(stateName, 0, 0f);
            animator.Update(0f);

            // 길이는 Play 직후 한 프레임 지나야 제대로 읽힌다.
            yield return null;
            float dur = animator.GetCurrentAnimatorStateInfo(0).length;
            if (dur <= 0f) dur = fallbackDuration;

            float t = 0f;
            while (t < dur) { t += Time.deltaTime; yield return null; }
        }

        if (animator != null) animator.enabled = false;
        doorObject.SetActive(false);
        _playing = null;
    }
}
