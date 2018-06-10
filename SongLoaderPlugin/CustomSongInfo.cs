using System;
using System.IO;

namespace SongLoaderPlugin
{
	[Serializable]
	public class CustomSongInfo
	{
		public string songName = "Missing name";
		public string songSubName = string.Empty;
		public string authorName = string.Empty;
		public float beatsPerMinute = 100;
		public float previewStartTime = 12;
		public float previewDuration = 10;
		public string environmentName = "DefaultEnvironment";
		public string coverImagePath = "cover.jpg";
		public string audioPath;
		public string videoPath;
		public int gamemodeType = 0;
		public float noteHitVolume = 1;
		public float noteMissVolume = 1;
		public DifficultyLevel[] difficultyLevels;
		public string path;
		public string levelId;

		[Serializable]
		public class DifficultyLevel
		{
			public string difficulty;
			public int difficultyRank;
			[Obsolete("audioPath has been moved to the song info. " +
			          "If the song audioPath is empty, it will try to use the audioPath in the first difficulty it finds.")]
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

		public string GetAudioPath()
		{
			if (!string.IsNullOrEmpty(audioPath)) return audioPath;

			foreach (var difficultyLevel in difficultyLevels)
			{
				if (string.IsNullOrEmpty(difficultyLevel.audioPath)) continue;
				return difficultyLevel.audioPath;
			}

			return null;
		}
	}
}
