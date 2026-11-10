using System;
using Grimward.Domain.Data;
using UnityEngine;

namespace Grimward.Presentation.Data
{
    /// <summary>Resources/GameData/gamedata.json → GameDatabase. 검증 오류가 있으면 예외.</summary>
    public static class GameDataLoader
    {
        public const string ResourcePath = "GameData/gamedata";

        public static GameDatabase Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException($"Resources/{ResourcePath}.json이 없습니다.");
            var file = JsonUtility.FromJson<GameDataFile>(asset.text);
            var db = new GameDatabase(file);
            var errors = db.Validate();
            if (errors.Count > 0)
                throw new InvalidOperationException("게임 데이터 오류:\n" + string.Join("\n", errors));
            return db;
        }
    }
}
