namespace SongLoaderPlugin.OverrideClasses
{
	public class CustomLevelCollection : LevelCollectionsForGameplayModes.LevelCollectionForGameplayMode
	{
		public CustomLevelCollection(GameplayMode gameplayMode, StandardLevelCollectionSO newLevelCollection)
		{
			_levelCollection = newLevelCollection;
			_gameplayMode = gameplayMode;
		}
	}
}