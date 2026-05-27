using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 로딩 완료 후 첫 게스트에게 보여주는 컷씬 (LoadingScene 에 배치).
//
// steps 를 클릭 순서대로 진행한다. 각 step:
//   - image       : 이 단계에서 켤 이미지 GameObject (좌/우/전체 위치는 그 오브젝트 RectTransform 에 세팅)
//   - clearBefore : true 면 표시 전에 화면의 모든 컷씬 이미지를 끔 (장면 전환). false 면 기존 위에 누적(좌→우 등).
// 마지막 step 표시 후 한 번 더 클릭하면 onComplete (게임씬 이동).
//
// 예시 시퀀스: 좌1 → 우1(누적) → [clear]좌2 → 우2(누적) → [clear]전체1 → [clear]전체2 → [clear]전체3 → 게임씬.
// ⚠️ 이미지는 나중에 준비되면 각 step.image 의 Sprite 만 교체하면 됨. steps 가 비어있으면 컷씬 스킵.
//
// 클릭 입력은 New Input System(Mouse/Touchscreen) 사용.
public class CutsceneController : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        [Tooltip("이 단계에서 켤 이미지 (위치=좌/우/전체는 이 오브젝트 RectTransform 에 미리 세팅)")]
        public GameObject image;

        [Tooltip("표시 전에 기존 컷씬 이미지를 모두 끔 (새 장면 시작). 누적 표시면 false.")]
        public bool clearBefore;
    }

    [Tooltip("컷씬 전체를 감싸는 풀스크린 루트(시작 시 비활성). 비우면 이 GameObject 사용.")]
    public GameObject root;

    [Tooltip("클릭 순서대로 진행할 단계들. 비어있으면 컷씬 스킵.")]
    public List<Step> steps = new();

    [Tooltip("각 단계 표시 후 이 시간(초) 동안 클릭 무시 (오입력 방지).")]
    public float clickLockSeconds = 0.5f;

    Action _onComplete;
    int    _index;
    bool   _playing;
    float  _lockUntil;

    void Awake()
    {
        if (root == null) root = gameObject;
        root.SetActive(false);
        HideAllImages();
    }

    // 컷씬 재생. 마지막 단계 후 클릭하면(또는 steps 가 없으면 즉시) onComplete 호출.
    public void Play(Action onComplete)
    {
        _onComplete = onComplete;

        if (steps == null || steps.Count == 0)
        {
            Debug.Log("[Cutscene] steps 없음 — 컷씬 스킵");
            Finish();
            return;
        }

        root.SetActive(true);
        HideAllImages();
        _playing = true;
        _index = -1;
        Advance(); // 첫 단계 표시
    }

    void Update()
    {
        if (!_playing) return;
        if (Time.unscaledTime < _lockUntil) return;
        if (PressedThisFrame()) Advance();
    }

    void Advance()
    {
        _index++;

        // 마지막 단계까지 본 뒤 클릭 → 종료
        if (_index >= steps.Count)
        {
            Finish();
            return;
        }

        var step = steps[_index];
        if (step.clearBefore) HideAllImages();
        if (step.image != null) step.image.SetActive(true);
        _lockUntil = Time.unscaledTime + clickLockSeconds;
    }

    void HideAllImages()
    {
        if (steps == null) return;
        foreach (var s in steps)
            if (s != null && s.image != null) s.image.SetActive(false);
    }

    void Finish()
    {
        _playing = false;
        HideAllImages();
        if (root != null) root.SetActive(false);
        var cb = _onComplete;
        _onComplete = null;
        cb?.Invoke();
    }

    static bool PressedThisFrame()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
        return false;
    }
}
