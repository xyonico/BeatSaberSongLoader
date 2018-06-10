namespace SongLoaderPlugin.OverrideClasses
{
	public class CustomLevelCollection : LevelCollectionsForGameplayModes.LevelCollectionForGameplayMode
	{
		public CustomLevelCollection(GameplayMode gameplayMode, LevelCollectionStaticData newLevelCollection)
		{
			_levelCollection = newLevelCollection;
			_gameplayMode = gameplayMode;
		}
	}
}