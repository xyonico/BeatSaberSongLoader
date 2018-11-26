using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SongLoaderPlugin.OverrideClasses
{
	public class CustomLevelCollectionSO : LevelCollectionSO
	{
		private readonly List<LevelSO> _levelList = new List<LevelSO>();

		private static BeatmapCharacteristicSO _standardCharacteristic;
		private static BeatmapCharacteristicSO _oneSaberCharacteristic;
		private static BeatmapCharacteristicSO _noArrowsCharacteristic;

		public static CustomLevelCollectionSO ReplaceOriginal(LevelCollectionSO original)
		{
			var newCollection = CreateInstance<CustomLevelCollectionSO>();
			newCollection._levelList.AddRange(original.levels);
			newCollection.UpdateArray();

			newCollection.ReplaceReferences();

			foreach (var originalLevel in original.levels)
			{
				if (_standardCharacteristic == null)
				{
					_standardCharacteristic = originalLevel.beatmapCharacteristics.FirstOrDefault(x => x.characteristicName == "Standard");
				}
				
				if (_oneSaberCharacteristic == null)
				{
					_oneSaberCharacteristic = originalLevel.beatmapCharacteristics.FirstOrDefault(x => x.characteristicName == "One Saber");
				}
				
				if (_noArrowsCharacteristic == null)
				{
					_noArrowsCharacteristic = originalLevel.beatmapCharacteristics.FirstOrDefault(x => x.characteristicName == "No Arrows");
				}
			}

			return newCollection;
		}

		public void ReplaceReferences()
		{
			var soloFreePlay = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().FirstOrDefault();
			if (soloFreePlay != null)
			{
				soloFreePlay.SetPrivateField("_levelCollection", this);
			}
			
			var partyFreePlay = Resources.FindObjectsOfTypeAll<PartyFreePlayFlowCoordinator>().FirstOrDefault();
			if (partyFreePlay != null)
			{
				partyFreePlay.SetPrivateField("_levelCollection", this);
			}
		}

		public void AddCustomLevels(IEnumerable<CustomLevel> customLevels)
		{
			foreach (var customLevel in customLevels)
			{
				var characteristics = new List<BeatmapCharacteristicSO> {_standardCharacteristic, _noArrowsCharacteristic};

				if (customLevel.customSongInfo.oneSaber)
				{
					characteristics.Add(_oneSaberCharacteristic);
				}
			
				customLevel.SetBeatmapCharacteristics(characteristics.ToArray());
				
				_levelList.Add(customLevel);
			}
			
			UpdateArray();
		}
		
		public void AddCustomLevel(CustomLevel customLevel)
		{
			var characteristics = new List<BeatmapCharacteristicSO> {_standardCharacteristic, _noArrowsCharacteristic};

			if (customLevel.customSongInfo.oneSaber)
			{
				characteristics.Add(_oneSaberCharacteristic);
			}
			
			customLevel.SetBeatmapCharacteristics(characteristics.ToArray());
			
			_levelList.Add(customLevel);
			
			UpdateArray();
		}

		public bool RemoveLevel(LevelSO level)
		{
			var removed = _levelList.Remove(level);

			if (removed)
			{
				UpdateArray();
			}

			return removed;
		}

		private void UpdateArray()
		{
			_levels = _levelList.ToArray();
		}
	}
}