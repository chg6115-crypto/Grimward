using System;
using System.Collections;
using Grimward.Domain.Combat;
using Grimward.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Grimward.Presentation.Battle
{
    /// <summary>진형 한 칸: 몸(임시 사각형) · 이름 · HP 막대 · 상태. 누르면 OnClicked.</summary>
    public sealed class CombatantSlotView : MonoBehaviour
    {
        public enum Highlight { None, Actor, EnemyTarget, AllyTarget, Inspected }

        [SerializeField] private Button button;
        [SerializeField] private Image frame;
        [SerializeField] private Image body;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Color partyBodyColor = new Color(0.25f, 0.3f, 0.38f);
        [SerializeField] private Color enemyBodyColor = new Color(0.38f, 0.22f, 0.22f);

        public event Action OnClicked;
        /// <summary>상태 표시(▲▼독)에 포인터가 올라감 / 나감.</summary>
        public event Action<RectTransform> OnStatusHover;
        public event Action OnStatusHoverEnd;
        public Combatant Bound { get; private set; }
        /// <summary>떠오르는 글자를 붙일 자리 (몸통).</summary>
        public RectTransform BodyRect => body.rectTransform;

        [Header("연출")]
        [SerializeField] private Color hitColor = new Color(1f, 0.35f, 0.3f);
        private Coroutine anim;
        private Color bodyBaseColor = Color.white;
        private bool showsSprite;

        /// <summary>에디터 UI 빌더 전용: 자식 참조 연결.</summary>
        public void Assign(Button button, Image frame, Image body, TMP_Text nameText, Image hpFill, TMP_Text hpText,
            TMP_Text statusText)
        {
            this.button = button; this.frame = frame; this.body = body; this.nameText = nameText;
            this.hpFill = hpFill; this.hpText = hpText; this.statusText = statusText;
        }

        private void Awake()
        {
            button.onClick.AddListener(() => OnClicked?.Invoke());
            statusText.raycastTarget = true; // 클릭은 부모 버튼으로 올라간다
            var relay = statusText.GetComponent<HoverRelay>();
            if (relay == null) relay = statusText.gameObject.AddComponent<HoverRelay>();
            relay.Entered += () => { if (Bound != null && Bound.Effects.All.Count > 0) OnStatusHover?.Invoke(statusText.rectTransform); };
            relay.Exited += () => OnStatusHoverEnd?.Invoke();
        }

        /// <summary>sprite가 있으면 몸통 자리에 그림을, 없으면 색 사각형과 이름을 그린다.</summary>
        public void Bind(Combatant c, Highlight highlight, Sprite sprite = null)
        {
            StopAnim();
            Bound = c;
            bool visible = c != null && c.IsAlive;
            body.enabled = visible;
            nameText.enabled = visible;
            hpFill.transform.parent.gameObject.SetActive(visible);
            hpText.enabled = visible;
            statusText.enabled = visible;
            button.interactable = visible;
            frame.enabled = visible && highlight != Highlight.None;
            if (!visible) return;

            if (sprite != null)
            {
                body.sprite = sprite;
                body.color = Color.white;
                body.preserveAspect = true;
                nameText.enabled = false;
                hpText.text = $"{c.Name}  {c.Hp}/{c.MaxHp}";
            }
            else
            {
                body.sprite = null;
                body.color = c.Side == BattleSide.Party ? partyBodyColor : enemyBodyColor;
                body.preserveAspect = false;
                nameText.text = c.Name;
                hpText.text = $"{c.Hp}/{c.MaxHp}";
            }
            showsSprite = sprite != null;
            bodyBaseColor = body.color;
            UpdateHp(c);
            statusText.text = BattleText.StatusIcons(c);
            frame.color = BattleText.HighlightColor(highlight);
        }

        /// <summary>HP 막대·글자만 다시 그린다 (연출 중, 쓰러진 대상도 0으로).</summary>
        public void UpdateHp(Combatant c)
        {
            if (c == null) return;
            hpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)c.Hp / c.MaxHp), 1f);
            hpText.text = showsSprite ? $"{c.Name}  {c.Hp}/{c.MaxHp}" : $"{c.Hp}/{c.MaxHp}";
        }

        // ── 연출 (몸통만 움직인다. dir: 상대 쪽 방향 +1 오른쪽 / −1 왼쪽) ──

        public void PlayLunge(float dir, float time) => Run(Lunge(dir, time));
        public void PlayHit(float time) => Run(Hit(time));
        public void PlayDodge(float dir, float time) => Run(Dodge(dir, time));
        public void PlayDeath(float time) => Run(Death(time));

        private void Run(IEnumerator routine)
        {
            if (!isActiveAndEnabled || !body.enabled) return;
            StopAnim();
            anim = StartCoroutine(routine);
        }

        private void StopAnim()
        {
            if (anim != null) StopCoroutine(anim);
            anim = null;
            body.rectTransform.anchoredPosition = Vector2.zero;
            if (body.enabled) body.color = bodyBaseColor;
        }

        private IEnumerator Lunge(float dir, float time)
        {
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                body.rectTransform.anchoredPosition = new Vector2(dir * 30f * Mathf.Sin(t / time * Mathf.PI), 0f);
                yield return null;
            }
            body.rectTransform.anchoredPosition = Vector2.zero;
        }

        private IEnumerator Hit(float time)
        {
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                body.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t * 70f) * 9f * (1f - k), 0f);
                body.color = Color.Lerp(hitColor, bodyBaseColor, k);
                yield return null;
            }
            body.rectTransform.anchoredPosition = Vector2.zero;
            body.color = bodyBaseColor;
        }

        private IEnumerator Dodge(float dir, float time)
        {
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                float s = Mathf.Sin(t / time * Mathf.PI);
                body.rectTransform.anchoredPosition = new Vector2(-dir * 26f * s, 0f);
                var c = bodyBaseColor; c.a = bodyBaseColor.a * (1f - 0.5f * s);
                body.color = c;
                yield return null;
            }
            body.rectTransform.anchoredPosition = Vector2.zero;
            body.color = bodyBaseColor;
        }

        private IEnumerator Death(float time)
        {
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                var c = bodyBaseColor; c.a = bodyBaseColor.a * (1f - t / time);
                body.color = c;
                yield return null;
            }
            var end = bodyBaseColor; end.a = 0f;
            body.color = end;   // 다음 Refresh에서 칸이 정리된다
        }

        // ── 큰 몬스터: 여러 칸에 걸쳐 그린다 ──
        private bool baseCached;
        private Vector2 baseMin, baseMax, bodyMin, bodyMax, frameMax;

        /// <summary>칸 너비를 [min, max]로 바꾸고, 차지 칸 수에 맞춰 몸통을 위로 키운다 (대충 맞춤).</summary>
        public void SetSpan(Vector2 anchorMin, Vector2 anchorMax, int size)
        {
            CacheBase();
            var rect = (RectTransform)transform;
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            float extra = 0.3f * (size - 1);
            body.rectTransform.anchorMin = bodyMin;
            body.rectTransform.anchorMax = new Vector2(bodyMax.x, bodyMax.y + extra);
            frame.rectTransform.anchorMax = new Vector2(frameMax.x, frameMax.y + extra);
        }

        public Vector2 BaseAnchorMin { get { CacheBase(); return baseMin; } }
        public Vector2 BaseAnchorMax { get { CacheBase(); return baseMax; } }

        private void CacheBase()
        {
            if (baseCached) return;
            var rect = (RectTransform)transform;
            baseMin = rect.anchorMin; baseMax = rect.anchorMax;
            bodyMin = body.rectTransform.anchorMin; bodyMax = body.rectTransform.anchorMax;
            frameMax = frame.rectTransform.anchorMax;
            baseCached = true;
        }
    }
}
