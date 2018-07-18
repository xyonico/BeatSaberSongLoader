using System.Collections.Generic;

namespace SongLoaderPlugin.OverrideClasses
{
	public class CustomLevelCollectionSO : StandardLevelCollectionSO
	{
		public List<StandardLevelSO> LevelList { get; private set; }
		
		public void Init(StandardLevelSO[] newLevels)
		{
			LevelList = new List<StandardLevelSO>(newLevels);
		}
	}
}