using System;

namespace SongLoaderPlugin.OverrideClasses
{
	public class CustomLevelCollectionStaticData : LevelCollectionStaticData
	{
		public void Init(LevelStaticData[] newLevelsData)
		{
			_levelsData = newLevelsData;
		}
	}
}