namespace SongLoaderPlugin.OverrideClasses
{
	public class CustomLevelCollectionsForGameplayModes : LevelCollectionsForGameplayModes
	{
		public override StandardLevelSO[] GetLevels(GameplayMode gameplayMode)
		{
			foreach (var levelCollectionForGameplayMode in _collections)
			{
				if (levelCollectionForGameplayMode.gameplayMode == gameplayMode)
				{
					var customLevelCollections = levelCollectionForGameplayMode as CustomLevelCollectionForGameplayMode;
					if (customLevelCollections != null)
					{
						var customLevelCollection = customLevelCollections.levelCollection as CustomLevelCollectionSO;
						if (customLevelCollection != null)
						{
							return customLevelCollection.LevelList.ToArray();
						}
					}
					return levelCollectionForGameplayMode.levelCollection.levels;
				}
			}
			return null;
		}

		public void SetCollections(LevelCollectionForGameplayMode[] levelCollectionForGameplayModes)
		{
			_collections = levelCollectionForGameplayModes;
		}
	}
}