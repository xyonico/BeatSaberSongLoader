using System;
using System.Linq;
using UnityEngine;

namespace SongLoaderPlugin
{
	/// There's a bug in Beat Saber, where
	/// if you pause a song mid-playthrough,
	/// and go back to menu, start playing another song,
	/// the new level difficulty isn't updated throughout all the game managers.
	/// So when you play a custom song that only has Expert difficulty,
	/// pause and go play another custom song that only has Easy difficulty,
	/// you're going to have problems.
	/// So here instead of returning null when it can't find any difficulty level,
	/// We're going to return the first difficulty level.

	public class CustomLevelStaticData : LevelStaticData
	{
		/*public override IDifficultyLevel GetDifficultyLevel(LevelDifficulty difficulty)
		{
			foreach (var difficultyLevel in _difficultyLevels)
			{
				if (difficultyLevel.difficulty == difficulty)
				{
					return difficultyLevel;
				}
			}
			
			return difficultyLevels.First();
		}*/

		public override void OnEnable()
		{
			if (_difficultyLevels == null) return;
			base.OnEnable();
		}

		public void Init(string newLevelId, string newSongName, string newSongSubName, string newAuthorName,
			float newBeatsPerMinute, float newPreviewStartTime, float newPreviewDuration,
			SceneInfo newSceneInfo, DifficultyLevel[] newDifficultyLevels)
		{
			_levelId = newLevelId;
			_songName = newSongName;
			_songSubName = newSongSubName;
			_authorName = newAuthorName;
			_beatsPerMinute = newBeatsPerMinute;
			_previewStartTime = newPreviewStartTime;
			_previewDuration = newPreviewDuration;
			_environmentSceneInfo = newSceneInfo;
			_difficultyLevels = newDifficultyLevels;
		}
	}
}