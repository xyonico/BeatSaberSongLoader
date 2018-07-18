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