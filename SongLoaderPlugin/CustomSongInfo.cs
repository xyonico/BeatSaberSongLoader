using System;
using System.IO;

namespace SongLoaderPlugin
{
	[Serializable]
	public class CustomSongInfo
	{
		public string songName = "Missing name";
		public string songSubName = string.Empty;
		[Obsolete("This has been renamed to songAuthorName to match game implementation")]
		public string authorName = string.Empty;
		public string songAuthorName;
		public float beatsPerMinute = 100;
		public float previewStartTime = 12;
		public float previewDuration = 10;
		public float songTimeOffset;
		public float shuffle;
		public float shufflePeriod;
		public string environmentName = "DefaultEnvironment";
		public string audioPath;
		public string coverImagePath = "cover.jpg";
		public bool oneSaber;
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
            public int noteJumpStartBeatOffset;
			public float noteJumpMovementSpeed;
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
			levelId = hash + "∎" + string.Join("∎", songName, songSubName, GetSongAuthor(), beatsPerMinute.ToString()) + "∎";
			return levelId;
		}

		public string GetSongAuthor()
		{
			if (songAuthorName == null)
			{
				songAuthorName = authorName;
			}

			return songAuthorName;
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
