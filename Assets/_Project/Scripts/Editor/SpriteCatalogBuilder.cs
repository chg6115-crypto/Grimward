using System.Collections.Generic;
using System.IO;
using System.Linq;
using Grimward.Presentation.Data;
using UnityEditor;
using UnityEngine;

namespace Grimward.EditorTools
{
    /// <summary>
    /// Art 폴더를 훑어 파일 이름으로 SpriteCatalog를 채운다.
    /// 규칙: Characters/Monsters/mon_&lt;id&gt;_body (또는 &lt;id&gt;) · Characters/Heroes/hero_&lt;id&gt;_body · hero_&lt;id&gt;_portrait
    ///       UI/Icons/Skills/skill_&lt;id&gt; · UI/Icons/Items/item_&lt;id&gt;
    /// </summary>
    public sealed class SpriteCatalogBuilder : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/_Project/Art/";
        private const string CatalogPath = "Assets/_Project/Data/Resources/SpriteCatalog.asset";
        private static bool scheduled;

        [MenuItem("Grimward/스프라이트 목록 갱신", priority = 21)]
        public static void Rebuild()
        {
            var entries = new List<SpriteCatalog.Entry>();
            Collect(entries, "Characters/Monsters", "mon", name => Strip(name, "mon_", "_body"));
            Collect(entries, "Characters/Heroes", null, HeroKey);
            Collect(entries, "UI/Icons/Skills", "skill", name => Strip(name, "skill_", null));
            Collect(entries, "UI/Icons/Items", "item", name => Strip(name, "item_", null));

            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(CatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
                catalog = ScriptableObject.CreateInstance<SpriteCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.SetEntries(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"Grimward: 스프라이트 목록 {entries.Count}개 — " + string.Join(", ", entries.Select(e => e.key)));
        }

        private static void Collect(List<SpriteCatalog.Entry> entries, string folder, string prefix,
            System.Func<string, string> keyOf)
        {
            string dir = ArtRoot + folder;
            if (!AssetDatabase.IsValidFolder(dir)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogWarning($"Grimward: {path} 는 Sprite로 가져오지 않았습니다 (Texture Type = Sprite (2D and UI)).");
                    continue;
                }
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                string key = keyOf(name);
                if (string.IsNullOrEmpty(key)) continue;
                if (prefix != null) key = prefix + ":" + key;
                if (entries.Any(e => e.key == key))
                {
                    Debug.LogWarning($"Grimward: 스프라이트 키 중복 {key} ({path}) — 먼저 찾은 것을 씁니다.");
                    continue;
                }
                entries.Add(new SpriteCatalog.Entry { key = key, sprite = sprite });
            }
        }

        private static string Strip(string name, string head, string tail)
        {
            if (head != null && name.StartsWith(head)) name = name.Substring(head.Length);
            if (tail != null && name.EndsWith(tail)) name = name.Substring(0, name.Length - tail.Length);
            return name;
        }

        private static string HeroKey(string name)
        {
            string id = Strip(name, "hero_", null);
            if (id.EndsWith("_portrait")) return "portrait:" + id.Substring(0, id.Length - "_portrait".Length);
            return "hero:" + Strip(id, null, "_body");
        }

        [InitializeOnLoadMethod]
        private static void CreateIfMissing()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<SpriteCatalog>(CatalogPath) == null) Rebuild();
            };
        }

        // 이미지를 넣거나 지우거나 옮기면 자동으로 갱신
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touched = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(p => p.StartsWith(ArtRoot) && !p.StartsWith(ArtRoot + "Fonts"));
            if (!touched || scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += () => { scheduled = false; Rebuild(); };
        }
    }
}
