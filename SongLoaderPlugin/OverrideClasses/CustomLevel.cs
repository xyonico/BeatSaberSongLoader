using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SongLoaderPlugin.OverrideClasses
{
	public class CustomLevel : StandardLevelSO
	{	
		public CustomSongInfo customSongInfo { get; private set; }
		public bool AudioClipLoading { get; set; }
		
		public void Init(CustomSongInfo newCustomSongInfo)
		{
			customSongInfo = newCustomSongInfo;
			_levelID = customSongInfo.GetIdentifier();
			_songName = customSongInfo.songName;
			_songSubName = customSongInfo.songSubName;
			_songAuthorName = customSongInfo.GetSongAuthor();
			_beatsPerMinute = customSongInfo.beatsPerMinute;
			_songTimeOffset = customSongInfo.songTimeOffset;
			_shuffle = customSongInfo.shuffle;
			_shufflePeriod = customSongInfo.shufflePeriod;
			_previewStartTime = customSongInfo.previewStartTime;
			_previewDuration = customSongInfo.previewDuration;
			_environmentSceneInfo = LoadSceneInfo(customSongInfo.environmentName);
			
			FixBPM();
		}

		public void SetAudioClip(AudioClip newAudioClip)
		{
			_audioClip = newAudioClip;
		}

		public void SetCoverImage(Sprite newCoverImage)
		{
			_coverImage = newCoverImage;
		}

		public void SetDifficultyBeatmaps(DifficultyBeatmap[] newDifficultyBeatmaps)
		{
			_difficultyBeatmaps = newDifficultyBeatmaps;
		}
		
		private static SceneInfo LoadSceneInfo(string environmentName)
		{
			var sceneInfo = Resources.Load<SceneInfo>("SceneInfo/" + environmentName + "SceneInfo");
			return sceneInfo == null ? Resources.Load<SceneInfo>("SceneInfo/DefaultEnvironmentSceneInfo") : sceneInfo;
		}

		private void FixBPM()
		{
			var bpms = new Dictionary<float, int> {{_beatsPerMinute, 0}};
			foreach (var diffLevel in customSongInfo.difficultyLevels)
			{
				if (string.IsNullOrEmpty(diffLevel.json)) continue;
				var bpm = GetBPMFromJson(diffLevel.json);
				if (bpm > 0)
				{
					if (bpms.ContainsKey(bpm))
					{
						bpms[bpm]++;
						continue;
					}
					bpms.Add(bpm, 1);
				}
			}

			_beatsPerMinute = bpms.OrderByDescending(x => x.Value).First().Key;
		}

		//This is quicker than using a JSON parser
		private float GetBPMFromJson(string json)
		{
			var split = json.Split(':');
			for (var i = 0; i < split.Length; i++)
			{
				if (!split[i].Contains("_beatsPerMinute")) continue;
				return Convert.ToSingle(split[i + 1].Split(',')[0]);
			}

			return 0;
		}
		
		public class CustomDifficultyBeatmap : DifficultyBeatmap
		{
			public CustomDifficultyBeatmap(IStandardLevel parentLevel, LevelDifficulty difficulty, int difficultyRank, BeatmapDataSO beatmapData) : base(parentLevel, difficulty, difficultyRank, beatmapData)
			{
			}

			public BeatmapDataSO BeatmapDataSO
			{
				get { return _beatmapData; }
			}
		}
	}
}