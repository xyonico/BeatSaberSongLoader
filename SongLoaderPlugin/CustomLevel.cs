using UnityEngine;

namespace SongLoaderPlugin
{
	public class CustomLevel : StandardLevelSO
	{	
		public void Init(CustomSongInfo customSongInfo)
		{
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
			return Resources.Load<SceneInfo>("SceneInfo/" + environmentName + "SceneInfo");
		}
	}
}