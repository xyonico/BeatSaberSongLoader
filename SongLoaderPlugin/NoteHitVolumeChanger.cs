using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace SongLoaderPlugin
{
	public static class NoteHitVolumeChanger
	{
		public static bool PrefabFound { get; private set; }
		private static NoteCutSoundEffect _noteCutSoundEffect;
		private static float _normalVolume;
		private static float _normalMissVolume;

		//Code snippet comes from Taz's NoteHitVolume plugin:
		//https://github.com/taz030485/NoteHitVolume/blob/master/NoteHitVolume/NoteHitVolume.cs
		public static void SetVolume(float hitVolume, float missVolume)
		{
			hitVolume = Mathf.Clamp01(hitVolume);
			missVolume = Mathf.Clamp01(missVolume);
			var pooled = false;
			if (_noteCutSoundEffect == null)
			{
				var noteCutSoundEffectManager = Resources.FindObjectsOfTypeAll<NoteCutSoundEffectManager>().FirstOrDefault();
				if (noteCutSoundEffectManager == null) return;
				_noteCutSoundEffect =
					noteCutSoundEffectManager.GetPrivateField<NoteCutSoundEffect>("_noteCutSoundEffectPrefab");
				pooled = true;
				PrefabFound = true;
			}

			if (_normalVolume == 0)
			{
				_normalVolume = _noteCutSoundEffect.GetPrivateField<float>("_goodCutVolume");
				_normalMissVolume = _noteCutSoundEffect.GetPrivateField<float>("_badCutVolume");
			}

			var newGoodVolume = _normalVolume * hitVolume;
			var newBadVolume = _normalMissVolume * missVolume;
			_noteCutSoundEffect.SetPrivateField("_goodCutVolume", newGoodVolume);
			_noteCutSoundEffect.SetPrivateField("_badCutVolume", newBadVolume);

			if (pooled)
			{
				var pool = Resources.FindObjectsOfTypeAll<NoteCutSoundEffect>();
				foreach (var effect in pool)
				{
					if (effect.name.Contains("Clone"))
					{
						effect.SetPrivateField("_goodCutVolume", newGoodVolume);
						effect.SetPrivateField("_badCutVolume", newBadVolume);
					}
				}
			}
		}
	}
}