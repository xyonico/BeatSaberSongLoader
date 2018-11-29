using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SongLoaderPlugin
{
	public static class EnvironmentsLoader
	{
		private static readonly List<SceneInfo> SceneInfos = new List<SceneInfo>();
		private const string DefaultEnvironmentName = "DefaultEnvironment";
		
		public static SceneInfo GetSceneInfo(string environmentName)
		{
			var sceneInfo = SceneInfos.FirstOrDefault(x => x.name == environmentName + "SceneInfo");
			if (sceneInfo != null) return sceneInfo;

			sceneInfo = Resources.FindObjectsOfTypeAll<SceneInfo>().FirstOrDefault(x => x.name == environmentName + "SceneInfo");
			if (sceneInfo == null)
			{
				Console.WriteLine("Failed to find scene info " + environmentName);
				return GetSceneInfo(DefaultEnvironmentName);
			}
			
			SceneInfos.Add(sceneInfo);
			return sceneInfo;
		}
	}
}
