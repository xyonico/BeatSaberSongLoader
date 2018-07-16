using System;

namespace SongLoaderPlugin.OverrideClasses
{
	public class CustomLevelCollectionStaticData : StandardLevelCollectionSO
	{
		public void Init(StandardLevelSO[] newLevels)
		{
			_levels = newLevels;
		}
	}
}