using System.Collections;
using Grimward.Domain.Core;
using Grimward.Domain.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Grimward.Presentation.Common
{
    /// <summary>
    /// 24시간 원형 시계 (몬스터 헌터식 분침 표현). 위쪽이 정오(12시), 아래쪽이 자정.
    /// 낮(dayStart~nightStart)은 원판 바탕색, 밤 구간은 어두운 호(Image Filled Radial360)로 덮는다.
    /// 바늘은 1시간 단위로 움직이고, 시간이 흐르면 회전 연출을 한다.
    /// </summary>
    public sealed class ClockView : MonoBehaviour
    {
        [SerializeField] private RectTransform hand;
        [SerializeField] private Image nightArc;
        [SerializeField] private TMP_Text label;
        [SerializeField] private float turnSeconds = 0.45f;

        private float shownAngle;
        private bool initialized;
        private Coroutine turning;

        public void Assign(RectTransform handRect, Image night, TMP_Text text)
        {
            hand = handRect; nightArc = night; label = text;
        }

        /// <summary>정오(720분)를 위(0도)로 두고 시계 방향으로 도는 각도. Unity UI는 반시계가 +이므로 음수.</summary>
        private static float AngleOf(int minuteOfDay) => -(minuteOfDay - 720) / 1440f * 360f;

        public void Set(GameTime time, BalanceConfig config, bool animate = true)
        {
            if (time == null) return;
            if (nightArc != null && config != null)
            {
                int nightLength = (config.dayStartMinute + GameTime.MinutesPerDay - config.nightStartMinute) % GameTime.MinutesPerDay;
                nightArc.type = Image.Type.Filled;
                nightArc.fillMethod = Image.FillMethod.Radial360;
                nightArc.fillOrigin = (int)Image.Origin360.Top;
                nightArc.fillClockwise = true;
                nightArc.fillAmount = nightLength / (float)GameTime.MinutesPerDay;
                nightArc.rectTransform.localEulerAngles = new Vector3(0, 0, AngleOf(config.nightStartMinute));
            }
            if (label != null)
            {
                string phase = config != null && time.IsNight(config) ? "밤" : "낮";
                label.text = $"{time.Day}일차 {time.MinuteOfDay / 60:00}:{time.MinuteOfDay % 60:00} · {phase}";
            }
            if (hand == null) return;
            float target = AngleOf(time.MinuteOfDay / 60 * 60); // 1시간 단위
            if (!initialized || !animate || !isActiveAndEnabled)
            {
                shownAngle = target;
                hand.localEulerAngles = new Vector3(0, 0, target);
                initialized = true;
                return;
            }
            // 시간은 앞으로만 흐르므로 시계 방향(각도 감소)으로, 필요한 만큼만 돈다 (0~360° 미만).
            float from = hand.localEulerAngles.z;
            float delta = Mathf.Repeat(from - target, 360f);
            if (delta < 0.01f) { shownAngle = target; return; }   // 시각이 그대로면 움직이지 않는다
            if (turning != null) StopCoroutine(turning);
            turning = StartCoroutine(Turn(from, from - delta));
        }

        private IEnumerator Turn(float from, float target)
        {
            for (float t = 0; t < turnSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / turnSeconds);
                hand.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(from, target, k));
                yield return null;
            }
            shownAngle = Mathf.Repeat(target, 360f);
            hand.localEulerAngles = new Vector3(0, 0, shownAngle);
            turning = null;
        }
    }
}
