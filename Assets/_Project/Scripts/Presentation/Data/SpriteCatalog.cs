using System;
using System.Collections.Generic;
using UnityEngine;

namespace Grimward.Presentation.Data
{
    /// <summary>
    /// 데이터 ID → 스프라이트 목록. 키 형식: "mon:goblin", "hero:swordsman", "portrait:swordsman", "skill:slash", "item:potion".
    /// 에디터가 Art 폴더의 파일 이름으로 자동으로 채운다 (메뉴 Grimward → 스프라이트 목록 갱신, 또는 이미지를 넣을 때 자동).
    /// </summary>
    [CreateAssetMenu(menuName = "Grimward/Sprite Catalog", fileName = "SpriteCatalog")]
    public sealed class SpriteCatalog : ScriptableObject
    {
        public const string ResourcePath = "SpriteCatalog";

        [Serializable]
        public sealed class Entry
        {
            public string key;
            public Sprite sprite;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        private Dictionary<string, Sprite> lookup;

        public IReadOnlyList<Entry> Entries => entries;

        public Sprite Get(string key)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, Sprite>();
                foreach (var e in entries)
                    if (e != null && !string.IsNullOrEmpty(e.key) && e.sprite != null) lookup[e.key] = e.sprite;
            }
            return key != null && lookup.TryGetValue(key, out var s) ? s : null;
        }

        public Sprite Monster(string id) => Get("mon:" + id);
        public Sprite Hero(string id) => Get("hero:" + id);

        /// <summary>에디터 빌더 전용.</summary>
        public void SetEntries(List<Entry> list)
        {
            entries = list;
            lookup = null;
        }

        public static SpriteCatalog Load() => Resources.Load<SpriteCatalog>(ResourcePath);
    }
}
