using System;
using System.IO;
using System.Security.Cryptography;

namespace SongLoaderPlugin
{
	[Serializable]
	public class CustomSongInfo : IEquatable<CustomSongInfo>
	{
		public string songName;
		public string songSubName;
		public string authorName;
		public float beatsPerMinute;
		public float previewStartTime;
		public float previewDuration;
		public string environmentName;
		public string coverImagePath;
		public string videoPath;
		public DifficultyLevel[] difficultyLevels;
		public string path;
		public string levelId;

		[Serializable]
		public class DifficultyLevel
		{
			public string difficulty;
			public int difficultyRank;
			public string audioPath;
			public string jsonPath;
			public string json;
		}

		public string GetIdentifier()
		{
			var combinedJson = "";
			foreach (var diffLevel in difficultyLevels)
			{
				if (!File.Exists(path + "/" + diffLevel.jsonPath))
				{
					continue;
				}
				
				diffLevel.json = File.ReadAllText(path + "/" + diffLevel.jsonPath);
				combinedJson += diffLevel.json;
			}

			var hash = Utils.CreateMD5FromString(combinedJson);
			levelId = hash + "∎" + string.Join("∎", new[] {songName, songSubName, authorName, beatsPerMinute.ToString()}) + "∎";
			return levelId;
		}

        public bool Equals(CustomSongInfo other)
        {
            return levelId == other.levelId;
        }
    }
}
