namespace SongLoaderPlugin.OverrideClasses
{
	public class CustomLevelCollectionForGameplayMode : LevelCollectionsForGameplayModes.LevelCollectionForGameplayMode
	{	
		public CustomLevelCollectionForGameplayMode(GameplayMode gameplayMode, StandardLevelCollectionSO newLevelCollection)
		{
			_levelCollection = newLevelCollection;
			_gameplayMode = gameplayMode;
		}
	}
}