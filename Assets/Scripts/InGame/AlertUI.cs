using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class AlertUI : MonoBehaviour
{
    public static AlertUI Instance { get; private set; }

    // ── AlertUI1 (기본) ──────────────────────────────────────────
    [Header("AlertUI1 - 기본")]
    public GameObject alertPanel;
    [Tooltip("AlertBtn/AlertTitlePanel/AlertTitleText — 제목 없으면 이 텍스트 오브젝트만 꺼짐(AlertTitlePanel 자체는 항상 유지)")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI messageText;
    public Button alertConfirmButton;   // 패널 전체를 덮는 투명 버튼

    // ── AlertUI7 (기존 AlertUI1 문구 — 랜덤이벤트 아닌 일반 Show(string) 전부 여기로, 2026-08-20) ──
    // AlertPanel1이 랜덤이벤트 결과 통합 문구(제목+pill) 전용으로 넘어가면서, 도전과제 공지 등 기존에
    // AlertPanel1을 쓰던 일반 안내 문구는 AlertPanel1을 복제한 이 패널로 옮김. 구조·필드는 AlertPanel1의
    // 예전(제목/pill 추가 전) 버전과 동일 — 그냥 message 한 줄만 있는 가장 단순한 패널.
    [Header("AlertUI7 - 기존 AlertUI1 자리(랜덤이벤트 아닌 일반 문구)")]
    public GameObject alertPanel7;
    public TextMeshProUGUI messageText7;
    public Button alertConfirmButton7;

    // ── AlertUI2 (돈) ────────────────────────────────────────────
    [Header("AlertUI2 - 돈")]
    public GameObject moneyPanel;
    public TextMeshProUGUI moneyMessageText;   // 상단 일반 메시지
    public GameObject moneyAmountRoot;          // G 아이콘 + 금액 묶음 (금액 없으면 숨김)
    public TextMeshProUGUI moneyAmountText;    // 금액 전용 텍스트
    public Button moneyConfirmButton;           // 패널 전체를 덮는 투명 버튼

    // ── AlertUI3 (직원 portrait) ─────────────────────────────────
    [Header("AlertUI3 - 직원 portrait")]
    public GameObject portraitPanel;
    public TextMeshProUGUI portraitLabelText;   // 특성명 / 이벤트명 / 직원이름
    public TextMeshProUGUI portraitMessageText;
    public Image portraitImage;
    public Button portraitConfirmButton;        // 패널 전체를 덮는 투명 버튼

    // ── AlertUI4 (결과 팝업 — 타이틀 + 결과 2줄) ──────────────────
    [Header("AlertUI4 - 결과 팝업 (Title+result1+result2)")]
    public GameObject result4Panel;
    public TextMeshProUGUI result4TitleText;
    public TextMeshProUGUI result4Text1;
    public TextMeshProUGUI result4Text2;
    public Button result4ConfirmButton;

    // ── AlertUI5 (결과 팝업 — 단순 문구) ──────────────────────────
    [Header("AlertUI5 - 결과 팝업 (TitleText만)")]
    public GameObject result5Panel;
    public TextMeshProUGUI result5TitleText;
    public Button result5ConfirmButton;

    // ── AlertUI6 (결과 팝업 — 타이틀 + 하단 문구) ─────────────────
    [Header("AlertUI6 - 결과 팝업 (Title+Bottom)")]
    public GameObject result6Panel;
    public TextMeshProUGUI result6TitleText;
    public TextMeshProUGUI result6BottomText;
    public Button result6ConfirmButton;

    // 결과 멘트 수치 강조색 — 좋다/나쁘다로 나눠 줄 전체를 칠하던 방식은 폐기(유저 요청, 2026-08-20).
    // 이제 버프/디버프 구분 없이 "+10%"/"-5" 같은 수치만 이 색으로 강조(ColorizeMentRich 참고).
    // DebuffMentColor는 AlertUI4/5/6(더 이상 아무도 안 부르는 죽은 case 분기)의 SetMentText만 아직 참조함.
    static readonly Color BuffMentColor   = new Color32(0xE6, 0x33, 0x56, 0xFF); // #E63356
    static readonly Color DebuffMentColor = new Color32(0x51, 0x7F, 0xFF, 0xFF); // #517FFF

    // ment1/2/3 텍스트에 박아넣는 pill 치환 토큰 — CSV 저자가 이 토큰을 그대로 써넣으면 표시 시점에
    // <sprite name="카테고리"> 인라인 아이콘으로 바뀐다. ProcessMentLines 참고.
    static readonly Dictionary<string, AlertPillCategory> PillTokens = new()
    {
        { "{만족도}",   AlertPillCategory.Satisfaction },
        { "{능력치}",   AlertPillCategory.Ability },
        { "{개발기간}", AlertPillCategory.DevPeriod },
        { "{기획점수}", AlertPillCategory.Planning },
        { "{개발점수}", AlertPillCategory.Programmer },
        { "{아트점수}", AlertPillCategory.Artist },
        { "{돈}",       AlertPillCategory.Money },
    };

    // ── AlertUI 통합(작업중) — b1~b8 목업 기준, 문장 중간에 pill(아이콘+라벨) 삽입 ──────
    [Header("통합 문구용 pill 세트 (작업중 — AlertUI1 패널을 통해 시험 렌더링)")]
    public AlertPillSet pillSet;

    // ── 내부 ────────────────────────────────────────────────────
    enum AlertType { Default, Money, Portrait, Result4, Result5, Result6, Legacy }

    struct Entry
    {
        public string        message;
        public System.Action onConfirm;
        public AlertType     type;
        public string        portraitId;
        public string        label;
        public int?          goldAmount;
        // AlertUI4/5/6 전용 — title=결과팝업멘트1, resultText1=멘트2, resultText2=멘트3(4만 사용)
        public string        title;
        public string        resultText1;
        public string        resultText2;
        // true면 ModalGate.WhenFree 대기 없이 즉시 표시 (다른 패널이 이미 게이트를 쥔 채로 열려있는 상황용)
        public bool          bypassGate;
        // 통합 문구(Show(List<AlertSegment>)) 전용 — 그 메시지에 삽입된 pill의 TMP_SpriteAsset(런타임 조립본).
        // Entry에 저장해야 큐잉 중인 다른 알림의 것과 안 섞인다(필드로 따로 빼면 나중에 표시될 때
        // 그 사이 다른 Show 호출이 값을 덮어써버리는 레이스가 생김).
        public TMP_SpriteAsset segmentSpriteAsset;
    }

    // pillSet 카테고리별로 런타임에 조립한 TMP_SpriteAsset 캐시 — GetOrBuildSpriteAsset 참고.
    readonly Dictionary<AlertPillCategory, TMP_SpriteAsset> _pillSpriteAssetCache = new();

    private Queue<Entry> _queue      = new();
    private bool         _isShowing  = false;
    // ShowNext()가 큐 맨 앞을 "처리 시작"했다고 _isShowing=true 로 표시한 시점과, 그게 실제로 화면에 뜬 시점은
    // 다르다 — ModalGate.WhenFree 대기 중이면 그 사이에 아직 아무것도 안 보인다. bypassGate 알림이 그 "대기
    // 중이라 아직 안 보이는" 앞선 알림 뒤에 그냥 큐잉되면, 앞선 알림이 뜰 때까지(다른 패널이 다 닫힐 때까지)
    // 덩달아 묶여서 훨씬 나중에 엉뚱한 타이밍에 튀어나온다 — bypassGate 의도(즉시 표시)가 깨짐.
    private bool         _isDisplayed = false;
    private System.Action _onConfirm;
    private AlertType     _currentType;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        alertPanel?.SetActive(false);
        alertPanel7?.SetActive(false);
        moneyPanel?.SetActive(false);
        portraitPanel?.SetActive(false);
        result4Panel?.SetActive(false);
        result5Panel?.SetActive(false);
        result6Panel?.SetActive(false);

        if (alertConfirmButton    != null) alertConfirmButton.onClick.AddListener(OnClickConfirm);
        if (alertConfirmButton7   != null) alertConfirmButton7.onClick.AddListener(OnClickConfirm);
        if (moneyConfirmButton    != null) moneyConfirmButton.onClick.AddListener(OnClickConfirm);
        if (portraitConfirmButton != null) portraitConfirmButton.onClick.AddListener(OnClickConfirm);
        if (result4ConfirmButton  != null) result4ConfirmButton.onClick.AddListener(OnClickConfirm);
        if (result5ConfirmButton  != null) result5ConfirmButton.onClick.AddListener(OnClickConfirm);
        if (result6ConfirmButton  != null) result6ConfirmButton.onClick.AddListener(OnClickConfirm);
    }

    // ── 공개 API ────────────────────────────────────────────────

    // bypassGate: true면 다른 패널(ItemPanel 등)이 이미 열려 게이트를 쥐고 있어도 대기 없이 바로 표시.
    // AlertPanel1이 랜덤이벤트 결과 통합 문구 전용으로 넘어가서, 그 외 일반 안내(도전과제 공지, 파산 안내 등)는
    // AlertPanel7(AlertUI7)로 뜬다 — 이 API 자체는 호출부 변경 없이 그대로 유지.
    public void Show(string message, System.Action onConfirm = null, bool bypassGate = false)
    {
        var entry = new Entry { message = message, onConfirm = onConfirm, type = AlertType.Legacy, bypassGate = bypassGate };
        if (bypassGate && _isShowing && !_isDisplayed) { DisplayEntry(entry); return; }
        _queue.Enqueue(entry);
        if (!_isShowing) ShowNext();
    }

    // 랜덤이벤트 결과 전용 — Show(string)과 동일하지만 AlertPanel1로 뜬다. ShowResult4/5/6(resultPopupType
    // 지정된 이벤트)과 달리 resultPopupType 미지정 fallback/직원도망 후폭풍/투자 이벤트 성공·실패처럼
    // "차트 팝업타입 없이 그냥 메시지 하나"인 랜덤이벤트 결과가 이 API를 쓴다 — 일반 Show(string)과 절대
    // 안 섞이게 반드시 이 메서드로 호출할 것(RandomEventChoiceUI.cs/RandomEventManager.cs 참고).
    // title(이벤트 이름)을 아는 호출부는 4-인자 오버로드로 넘길 것 — AlertTitleText에 그대로 표시된다.
    public void ShowRandomEventResult(string title, string message, System.Action onConfirm = null, bool bypassGate = false)
    {
        var entry = new Entry { message = message, onConfirm = onConfirm, type = AlertType.Default, bypassGate = bypassGate, title = title };
        if (bypassGate && _isShowing && !_isDisplayed) { DisplayEntry(entry); return; }
        _queue.Enqueue(entry);
        if (!_isShowing) ShowNext();
    }

    // 이벤트 이름을 모르는(또는 없는) 호출부용 — 제목 없이 본문만.
    public void ShowRandomEventResult(string message, System.Action onConfirm = null, bool bypassGate = false)
        => ShowRandomEventResult(null, message, onConfirm, bypassGate);

    // goldAmount: G 아이콘 옆 금액 표시. null이면 금액 영역 숨김.
    // bypassGate: true면 다른 패널(ProjectSetupUI 등)이 이미 열려 게이트를 쥐고 있어도 대기 없이 바로 표시.
    public void ShowMoney(string message, int? goldAmount = null, System.Action onConfirm = null, bool bypassGate = false)
    {
        var entry = new Entry { message = message, onConfirm = onConfirm, type = AlertType.Money, goldAmount = goldAmount, bypassGate = bypassGate };
        if (bypassGate && _isShowing && !_isDisplayed) { DisplayEntry(entry); return; }
        _queue.Enqueue(entry);
        if (!_isShowing) ShowNext();
    }

    // label: 특성명 / 이벤트명이면 그 이름, 아이템 발동이면 직원 이름
    // Portrait는 항상 ModalGate 대기 없이 즉시 표시되는 타입이라(ShowNext 참고) Show/ShowMoney의 bypassGate와
    // 동일하게, 앞서 대기 중인(아직 안 뜬) 알림에 밀리지 않도록 같은 새치기 처리를 적용한다.
    public void ShowPortrait(string message, string portraitId, string label, System.Action onConfirm = null)
    {
        var entry = new Entry { message = message, onConfirm = onConfirm, type = AlertType.Portrait, portraitId = portraitId, label = label };
        if (_isShowing && !_isDisplayed) { DisplayEntry(entry); return; }
        _queue.Enqueue(entry);
        if (!_isShowing) ShowNext();
    }

    // b1~b8 목업의 "문장 중 일부만 pill(아이콘+라벨)로 바뀐다" 구조를 그대로 옮긴 세그먼트 하나.
    // pill이 None이면 text를 그냥 이어붙이고, None이 아니면 text는 무시하고 그 카테고리의 pill을 삽입한다.
    public struct AlertSegment
    {
        public string text;
        public AlertPillCategory pill;
        public static AlertSegment Text(string t) => new AlertSegment { text = t, pill = AlertPillCategory.None };
        public static AlertSegment Pill(AlertPillCategory c) => new AlertSegment { pill = c };
    }

    // 통합 문구 표시 — 지금은 AlertUI1(기본 패널)을 그대로 재사용해 시험 렌더링한다(전용 패널은 아직 없음).
    // pillSet에 해당 카테고리의 TMP_SpriteAsset이 아직 없으면 그 세그먼트는 조용히 생략되고 텍스트만
    // 남는다 — 이미지가 순차적으로 채워지는 동안에도 항상 안전하게 동작.
    // ⚠️ 한 메시지에 서로 다른 pill 카테고리가 2개 이상 섞이면 마지막 것만 반영됨(TextMeshProUGUI.spriteAsset이
    // 텍스트 하나당 1개라 <sprite index=0>이 전부 그 에셋을 가리킴) — b1~b8 전부 pill 0~1개뿐이라 아직 문제 없음.
    public void Show(List<AlertSegment> segments, System.Action onConfirm = null, bool bypassGate = false)
    {
        string body = BuildBody(segments, out TMP_SpriteAsset spriteAsset);
        var entry = new Entry { message = body, onConfirm = onConfirm, type = AlertType.Default, bypassGate = bypassGate, segmentSpriteAsset = spriteAsset };
        if (bypassGate && _isShowing && !_isDisplayed) { DisplayEntry(entry); return; }
        _queue.Enqueue(entry);
        if (!_isShowing) ShowNext();
    }

    string BuildBody(List<AlertSegment> segments, out TMP_SpriteAsset spriteAsset)
    {
        spriteAsset = null;
        var sb = new System.Text.StringBuilder();
        if (segments != null)
        {
            foreach (var seg in segments)
            {
                if (seg.pill != AlertPillCategory.None)
                {
                    var asset = GetOrBuildSpriteAsset(seg.pill);
                    if (asset != null)
                    {
                        spriteAsset = asset;
                        sb.Append("<sprite index=0>");
                    }
                }
                else
                {
                    sb.Append(seg.text);
                }
            }
        }
        return sb.ToString();
    }

    // pillSet에 등록된 평범한 Sprite 하나로부터 그 카테고리 전용 TMP_SpriteAsset을 런타임에 조립해 캐싱한다
    // — 에디터에서 TMP Sprite Asset을 미리 만들어둘 필요 없이 SO에 Sprite만 꽂으면 바로 동작. 카테고리마다
    // 별도 에셋으로 만드는 이유: 배지 이미지들이 서로 다른(패킹 안 된) 개별 텍스처라, 하나의 spriteSheet를
    // 공유하는 아틀라스 방식(카테고리 여러 개를 한 TMP_SpriteAsset에 욱여넣는 방식)은 텍스처가 안 맞아서
    // 못 씀 — 카테고리당 텍스처 1장짜리 전용 에셋이면 이 문제가 애초에 안 생긴다.
    TMP_SpriteAsset GetOrBuildSpriteAsset(AlertPillCategory category)
    {
        if (_pillSpriteAssetCache.TryGetValue(category, out var cached)) return cached;

        var sprite = pillSet != null ? pillSet.Get(category) : null;
        if (sprite == null) { _pillSpriteAssetCache[category] = null; return null; }

        var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        asset.name = $"AlertPill_{category}_Runtime";
        asset.spriteSheet = sprite.texture;

        // glyphRect는 원본 텍스처의 UV 영역(픽셀 그대로) — 렌더 크기와는 별개. 렌더 크기(metrics)만 높이
        // 62로 고정하고 원본 가로세로 비율을 유지해 너비를 비례 계산(뱃지마다 원본 해상도가 달라도 인라인
        // 삽입됐을 때 전부 같은 높이로 보이게).
        const float PillRenderHeight = 62f;
        float pillRenderWidth = sprite.rect.height > 0f
            ? sprite.rect.width * (PillRenderHeight / sprite.rect.height)
            : PillRenderHeight;

        // bearingY(베이스라인 기준 아이콘 상단 높이)를 그냥 PillRenderHeight로 두면 아이콘이 베이스라인
        // 바로 위에서부터 전부 위로만 쌓여서 글자 세로 중앙보다 위쪽으로 떠 보인다. 텍스트 폰트의
        // ascent/descent 중간선(= 줄의 세로 중앙)에 아이콘의 세로 중앙이 오도록 bearingY를 계산해
        // 실제로 텍스트 줄 한가운데 정렬되게 한다. 폰트 정보를 못 구하면 기존 방식(위 정렬)으로 대체.
        float bearingY = PillRenderHeight;
        var font = messageText != null ? messageText.font : null;
        if (font != null)
        {
            float midline = (font.faceInfo.ascentLine + font.faceInfo.descentLine) / 2f;
            bearingY = midline + PillRenderHeight / 2f;
        }

        var glyph = new TMP_SpriteGlyph
        {
            index = 0,
            metrics = new UnityEngine.TextCore.GlyphMetrics(pillRenderWidth, PillRenderHeight, 0f, bearingY, pillRenderWidth),
            glyphRect = new UnityEngine.TextCore.GlyphRect((int)sprite.rect.x, (int)sprite.rect.y, (int)sprite.rect.width, (int)sprite.rect.height),
            scale = 1f,
            sprite = sprite,
        };
        var character = new TMP_SpriteCharacter(0, glyph) { name = category.ToString() };
        // spriteGlyphTable/spriteCharacterTable은 읽기전용 프로퍼티(내부 리스트 참조만 반환) — 새 리스트를
        // 통째로 대입할 수 없고, 반환된 리스트에 직접 Add해야 함.
        asset.spriteGlyphTable.Add(glyph);
        asset.spriteCharacterTable.Add(character);

        var shader = Shader.Find("TextMeshPro/Sprite");
        if (shader != null)
        {
            var mat = new Material(shader) { name = asset.name + " Material" };
            mat.SetTexture("_MainTex", asset.spriteSheet);
            asset.material = mat;
        }

        // m_Version이 비어있으면 UpdateLookupTables() 내부의 "구버전(spriteInfoList) → 신버전 업그레이드" 경로를
        // 타는데, 방금 만든 에셋은 그 구버전 데이터 자체가 없어서 TMP 내부에서 NullReferenceException이 난다
        // (CoinSpriteAssetCreator.cs의 SerializedObject 방식과 동일한 이유 — 여긴 런타임 코드라 UnityEditor를
        // 못 쓰니 리플렉션으로 private 필드에 직접 설정).
        var versionField = typeof(TMP_SpriteAsset).GetField("m_Version",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        versionField?.SetValue(asset, "1.1.0");

        asset.UpdateLookupTables();
        _pillSpriteAssetCache[category] = asset;
        return asset;
    }

    // 한 메시지(여러 줄)에 서로 다른 pill 카테고리가 섞일 때용 — 배지 이미지들이 서로 다른(패킹 안 된)
    // 개별 텍스처라 하나의 TMP_SpriteAsset(텍스처 1장짜리 material)에 욱여넣을 수 없다. 대신 TMP의
    // fallbackSpriteAssets 체인을 이용: "기본" 에셋 하나에 나머지를 fallback으로 매달아두면
    // <sprite name="X"> 태그가 체인을 따라가며 이름으로 찾아준다. 매 호출마다 다른 카테고리를 기본으로
    // 고르면 A.fallback에 B가, B.fallback에 A가 들어가는 순환참조가 생길 수 있어서, 항상 enum 순서가
    // 가장 낮은 카테고리를 기본으로 고정한다.
    TMP_SpriteAsset GetOrBuildSpriteAsset(List<AlertPillCategory> categories)
    {
        var sorted = new List<AlertPillCategory>(categories);
        sorted.Sort();
        TMP_SpriteAsset primary = null;
        foreach (var cat in sorted)
        {
            var asset = GetOrBuildSpriteAsset(cat);
            if (asset == null) continue;
            if (primary == null) { primary = asset; continue; }
            if (primary.fallbackSpriteAssets == null) primary.fallbackSpriteAssets = new List<TMP_SpriteAsset>();
            if (!primary.fallbackSpriteAssets.Contains(asset)) primary.fallbackSpriteAssets.Add(asset);
        }
        return primary;
    }

    // ShowResult4/5/6 전용 — 여러 결과멘트 줄에서 PillTokens 토큰을 <sprite name="카테고리"> 인라인
    // 아이콘으로 바꾸고(그 메시지에 실제 쓰인 카테고리들만 모아 fallback 체인 조립), 각 줄을
    // ColorizeMentRich로 감싼 뒤 줄바꿈으로 이어붙인다. 빈 줄은 건너뜀.
    (string body, TMP_SpriteAsset spriteAsset) ProcessMentLines(params string[] lines)
    {
        var used = new List<AlertPillCategory>();
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;
            foreach (var kv in PillTokens)
                if (line.Contains(kv.Key) && !used.Contains(kv.Value)) used.Add(kv.Value);
        }
        var asset = used.Count > 0 ? GetOrBuildSpriteAsset(used) : null;

        var parts = new List<string>();
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;
            string replaced = line;
            foreach (var kv in PillTokens)
                if (replaced.Contains(kv.Key))
                    replaced = replaced.Replace(kv.Key, $"<sprite name=\"{kv.Value}\">");
            parts.Add(ColorizeMentRich(replaced));
        }
        return (string.Join("\n", parts), asset);
    }

    // 결과 팝업 종류 1/2/3(전부 랜덤이벤트 전용, RandomEventChoiceUI만 호출) — 2026-08-20에 전용
    // AlertPanel4/5/6 대신 AlertPanel1(제목=AlertTitleText, 본문=AlertText)로 통합. AlertPanel4/5/6 자체는
    // 지우지 않음(유저 결정 — 특성/아이템 등 다른 패널은 안 건드리기로 함, [[project_alertui_consolidation]]
    // 참고) — 그냥 이 세 메서드가 더는 그쪽을 안 쓸 뿐.
    // eventTitle — 이벤트 이름(2026-08-20 추가). 예전엔 title 자리에 결과멘트1이 들어가고 실제로는
    // 결과멘트1/2/3 전부가 "결과 내용"이었는데, 이제 제목 슬롯을 실제 이벤트 이름 전용으로 바꾸면서 결과멘트도
    // 전부 본문으로 내려감 — 그래서 ShowResult4/6의 인자 개수가 하나씩 늘었음(호출부는 RandomEventChoiceUI.
    // ShowResultPopup/2 뿐이라 전부 같이 갱신함).
    // 각 줄은 +/- 등 내용에 따라 버프(#D22E2E)/디버프(#6251D4) 자동 색상(부호 없으면 기본색) — 줄마다
    // 색이 다를 수 있어 SetMentText(텍스트 오브젝트 전체 .color)가 아니라 줄마다 <color> 리치텍스트로 감쌈.
    public void ShowResult4(string eventTitle, string result1, string result2, string result3, System.Action onConfirm = null)
    {
        var (body, asset) = ProcessMentLines(result1, result2, result3);
        _queue.Enqueue(new Entry { message = body, onConfirm = onConfirm, type = AlertType.Default, title = eventTitle, segmentSpriteAsset = asset });
        if (!_isShowing) ShowNext();
    }

    // 결과 팝업 종류 2 — 제목(이벤트 이름) + 본문 한 줄.
    public void ShowResult5(string eventTitle, string result1, System.Action onConfirm = null)
    {
        var (body, asset) = ProcessMentLines(result1);
        _queue.Enqueue(new Entry { message = body, onConfirm = onConfirm, type = AlertType.Default, title = eventTitle, segmentSpriteAsset = asset });
        if (!_isShowing) ShowNext();
    }

    // 결과 팝업 종류 3 — 제목(이벤트 이름) + 본문 두 줄.
    public void ShowResult6(string eventTitle, string result1, string result2, System.Action onConfirm = null)
    {
        var (body, asset) = ProcessMentLines(result1, result2);
        _queue.Enqueue(new Entry { message = body, onConfirm = onConfirm, type = AlertType.Default, title = eventTitle, segmentSpriteAsset = asset });
        if (!_isShowing) ShowNext();
    }

    // ── 내부 큐 ─────────────────────────────────────────────────

    void ShowNext()
    {
        _isDisplayed = false;
        if (_queue.Count == 0) { _isShowing = false; return; }
        _isShowing = true;

        // Portrait(특성/이벤트 설명, 아이템 사용 결과 등)는 "지금 보고 있는 패널 위에 즉시 뜨는" 정보 팝업이라
        // 다른 UI 가 점유 중인 ModalGate 를 기다리지 않고 바로 표시한다(자기 자신도 아래서 ModalGate.Register 됨).
        // 안 그러면 예: HiringUI 가 ConfirmHirePanel 이 떠 있는 동안 게이트를 쥐고 있어서, 그 안에서 특성/이벤트
        // 버튼을 눌러도 안 뜨고 ConfirmHirePanel 이 닫혀 게이트가 풀리는 순간에야 뒤늦게 뜨는 문제가 있었음.
        if (_queue.Peek().type == AlertType.Portrait || _queue.Peek().bypassGate) { DisplayDequeued(); return; }

        ModalGate.I.WhenFree(DisplayDequeued);
    }

    void DisplayDequeued()
    {
        if (_queue.Count == 0) { _isShowing = false; return; }
        DisplayEntry(_queue.Dequeue());
    }

    // Show/ShowMoney/ShowPortrait의 "새치기" 경로와 DisplayDequeued 양쪽이 공유하는 실제 표시 로직.
    // 큐 상태(_queue/_isShowing)는 건드리지 않는다 — 새치기로 불렸을 때 큐에 이미 대기 중인(아직 안 뜬)
    // 앞선 알림은 그대로 큐에 남아 나중에 자기 차례(ModalGate 해제)가 오면 정상적으로 뒤이어 표시된다.
    void DisplayEntry(Entry entry)
    {
        _isShowing   = true;
        _isDisplayed = true;
        GameTimeManager.Instance?.StopTime();
        _onConfirm   = entry.onConfirm;
        _currentType = entry.type;

        switch (entry.type)
        {
            case AlertType.Money:
                if (moneyPanel == null) { Debug.LogError("[AlertUI] moneyPanel 미연결"); goto default; }
                if (moneyMessageText != null) moneyMessageText.text = entry.message;
                if (moneyAmountRoot != null)
                {
                    bool hasAmount = entry.goldAmount.HasValue;
                    moneyAmountRoot.SetActive(hasAmount);
                    if (hasAmount && moneyAmountText != null)
                        moneyAmountText.text = $"{entry.goldAmount.Value:N0} G";
                }
                EnsureTopMost(moneyPanel);
                moneyPanel.SetActive(true);
                break;

            case AlertType.Portrait:
                if (portraitPanel == null) { Debug.LogError("[AlertUI] portraitPanel 미연결"); goto default; }
                if (portraitLabelText   != null) portraitLabelText.text   = entry.label ?? "";
                if (portraitMessageText != null) portraitMessageText.text = entry.message;
                if (portraitImage != null)
                {
                    var sp = string.IsNullOrEmpty(entry.portraitId)
                        ? null
                        : Resources.Load<Sprite>($"Portraits/Mini/{entry.portraitId}");
                    portraitImage.sprite  = sp;
                    portraitImage.enabled = sp != null;
                }
                EnsureTopMost(portraitPanel);
                portraitPanel.SetActive(true);
                break;

            case AlertType.Result4:
                if (result4Panel == null) { Debug.LogError("[AlertUI] result4Panel 미연결"); goto default; }
                SetMentText(result4TitleText, entry.title);
                SetMentText(result4Text1,     entry.resultText1);
                SetMentText(result4Text2,     entry.resultText2);
                EnsureTopMost(result4Panel);
                result4Panel.SetActive(true);
                break;

            case AlertType.Result5:
                if (result5Panel == null) { Debug.LogError("[AlertUI] result5Panel 미연결"); goto default; }
                SetMentText(result5TitleText, entry.title);
                EnsureTopMost(result5Panel);
                result5Panel.SetActive(true);
                break;

            case AlertType.Result6:
                if (result6Panel == null) { Debug.LogError("[AlertUI] result6Panel 미연결"); goto default; }
                SetMentText(result6TitleText,  entry.title);
                SetMentText(result6BottomText, entry.resultText1);
                EnsureTopMost(result6Panel);
                result6Panel.SetActive(true);
                break;

            case AlertType.Legacy:
                // 기존 AlertPanel1 자리 — 랜덤이벤트가 아닌 일반 Show(string) 문구(도전과제 공지, 파산 안내 등)
                // 전용. AlertPanel1을 복제해 만든 AlertPanel7을 그대로 씀(제목/pill 없음, 메시지 한 줄만).
                if (alertPanel7 == null) { Debug.LogError("[AlertUI] alertPanel7 미연결"); goto default; }
                if (messageText7 != null) messageText7.text = entry.message;
                EnsureTopMost(alertPanel7);
                alertPanel7.SetActive(true);
                break;

            default:
                if (alertPanel == null) { Debug.LogError("[AlertUI] alertPanel 미연결"); return; }
                // 제목(entry.title) — ShowResult4/5/6(랜덤이벤트 결과 팝업, 2026-08-20에 AlertPanel1로 통합)이
                // 채워서 넘긴다. Show(segments)/ShowMoney 등은 title이 비어있음 — 이때 임시 제목을 채우지
                // 않고, AlertTitlePanel(레이아웃 슬롯)은 그대로 둔 채 AlertTitleText만 꺼서 숨긴다
                // (AlertTitlePanel을 끄면 안 됨 — 유저 지정).
                bool hasTitle = !string.IsNullOrEmpty(entry.title);
                if (titleText != null)
                {
                    titleText.gameObject.SetActive(hasTitle);
                    if (hasTitle) titleText.text = ColorizeMentRich(entry.title);
                }
                if (messageText != null)
                {
                    // segmentSpriteAsset이 없으면(일반 Show(string) 호출) 이전 통합 문구가 남긴 spriteAsset이
                    // 새어들어가지 않도록 항상 null로 명시 리셋.
                    messageText.spriteAsset = entry.segmentSpriteAsset;
                    messageText.text = entry.message;
                }
                EnsureTopMost(alertPanel);
                alertPanel.SetActive(true);
                break;
        }

        ModalGate.I.Register(this);
    }

    // AlertUI4/5/6 전용 — 텍스트를 채우고, 버프(+/증가/상승 등)면 D22E2E, 디버프(-/감소/하락/차감 등)면 6251D4로
    // 색을 바꾼다. 부호/키워드가 없거나 둘 다 섞여 있으면 인스펙터 기본색 유지(색 변경 안 함).
    void SetMentText(TextMeshProUGUI tmp, string content)
    {
        if (tmp == null) return;
        tmp.text = content ?? "";
        var color = DetectMentColor(content);
        if (color.HasValue) tmp.color = color.Value;
    }

    // 줄 전체를 버프(빨강)/디버프(파랑)로 나눠 칠하던 방식은 폐기 — 이제 좋다/나쁘다 구분 없이, 그 줄 안의
    // "+10%"/"-5" 같은 수치(부호+숫자, %는 선택)만 #E63356으로 강조하고 나머지 텍스트는 기본색 그대로 둔다.
    // 이미 자체 색상 태그가 있는 줄(코인 아이콘+금액처럼 런타임에서 이미 <color=...>로 감싸 넣은 경우)은
    // 그 안의 숫자까지 다시 감싸면 색 태그가 중첩되며 깨지므로 손대지 않고 그대로 통과시킨다.
    static readonly Regex NumericDeltaRegex = new(@"[+-]\d+(\.\d+)?%?", RegexOptions.Compiled);

    static string ColorizeMentRich(string content)
    {
        if (string.IsNullOrEmpty(content)) return content ?? "";
        if (content.Contains("<color=")) return content;
        return NumericDeltaRegex.Replace(content, m => $"<color=#{ColorUtility.ToHtmlStringRGB(BuffMentColor)}>{m.Value}</color>");
    }

    static Color? DetectMentColor(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        bool buff   = text.Contains("+") || text.Contains("증가") || text.Contains("상승");
        bool debuff = text.Contains("-") || text.Contains("감소") || text.Contains("하락") || text.Contains("차감");

        if (buff && !debuff)   return BuffMentColor;
        if (debuff && !buff)   return DebuffMentColor;
        return null; // 둘 다 있거나(예: "+10 / -5%") 둘 다 없으면 판단 보류 — 기본색 유지
    }

    void EnsureTopMost(GameObject panel)
    {
        if (panel == null) return;
        var canvas = panel.GetComponent<Canvas>();
        if (canvas == null) canvas = panel.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = 32000;
        if (panel.GetComponent<GraphicRaycaster>() == null)
            panel.AddComponent<GraphicRaycaster>();
        panel.transform.SetAsLastSibling();
    }

    // AlertUI1/2/3 각각의 확인 버튼에서 이 메서드를 호출
    public void OnClickConfirm()
    {
        switch (_currentType)
        {
            case AlertType.Money:    moneyPanel?.SetActive(false);    break;
            case AlertType.Portrait: portraitPanel?.SetActive(false); break;
            case AlertType.Result4:  result4Panel?.SetActive(false);  break;
            case AlertType.Result5:  result5Panel?.SetActive(false);  break;
            case AlertType.Result6:  result6Panel?.SetActive(false);  break;
            case AlertType.Legacy:   alertPanel7?.SetActive(false);   break;
            default:                 alertPanel?.SetActive(false);    break;
        }

        ModalGate.I.Unregister(this);
        GameTimeManager.Instance?.StartTime();
        var cb = _onConfirm;
        _onConfirm = null;
        cb?.Invoke();
        ShowNext();
    }
}
