using System;
using IllusionPlugin;
using UnityEngine;

namespace SongLoaderPlugin
{
	public class Plugin : IPlugin
	{
		public const string VersionNumber = "v5.0.2-beta";

		private SceneEvents _sceneEvents;
		
		public string Name
		{
			get { return "Song Loader Plugin"; }
		}

		public string Version
		{
			get { return VersionNumber; }
		}
		
		public void OnApplicationStart()
		{
			_sceneEvents = new GameObject("menu-signal").AddComponent<SceneEvents>();
			_sceneEvents.MenuSceneEnabled += OnMenuSceneEnabled;
		}

		private void OnMenuSceneEnabled()
		{
			SongLoader.OnLoad();
		}

		public void OnApplicationQuit()
		{
			PlayerPrefs.DeleteKey("lbPatched");
		}

		public void OnLevelWasInitialized(int level)
		{
		}

		public void OnUpdate()
		{
		}

		public void OnFixedUpdate()
		{	
		}

		public void OnLevelWasLoaded(int level)
		{	
		}
	}
}