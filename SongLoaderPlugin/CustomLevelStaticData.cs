using System.Linq;

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
		public override DifficultyLevel GetDifficultyLevel(Difficulty difficulty)
		{
			foreach (var difficultyLevel in _difficultyLevels)
			{
				if (difficultyLevel.difficulty == difficulty)
				{
					return difficultyLevel;
				}
			}
			
			return difficultyLevels.First();
		}
	}
}