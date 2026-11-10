using System;
using TMPro;
using UnityEngine;

namespace Grimward.Presentation.Common
{
    /// <summary>
    /// 한글 TMP 폰트. Inspector에 지정한 폰트가 있으면 그것을, 없으면 OS 한글 폰트로 동적 폰트를 만든다(PC 테스트용).
    /// 모바일 빌드 전에는 한글 TMP 폰트 에셋을 만들어 지정해야 한다.
    /// </summary>
    public static class FontProvider
    {
        private static TMP_FontAsset runtimeFont;

        public static TMP_FontAsset Resolve(TMP_FontAsset assigned)
        {
            if (assigned != null) return assigned;
            if (runtimeFont != null) return runtimeFont;
            foreach (string family in new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "NanumGothic" })
            {
                runtimeFont = TMP_FontAsset.CreateFontAsset(family, "Regular");
                if (runtimeFont != null) return runtimeFont;
            }
            throw new InvalidOperationException("한글 폰트를 찾지 못했습니다. Bootstrap의 Ui Font에 한글 TMP 폰트를 지정하세요.");
        }
    }
}
