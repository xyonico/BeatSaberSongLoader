using IllusionPlugin;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SongLoaderPlugin
{
	public class Plugin : IPlugin
	{
		public const string VersionNumber = "v4.3.2";
		
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
			SceneManager.sceneLoaded += SceneManagerOnSceneLoaded;
		}

		public void OnApplicationQuit()
		{
			PlayerPrefs.DeleteKey("lbPatched");
			SceneManager.sceneLoaded -= SceneManagerOnSceneLoaded;
		}

		private void SceneManagerOnSceneLoaded(Scene newScene, LoadSceneMode mode)
		{
			if (newScene.name != SongLoader.MenuSceneName) return;
			SongLoader.OnLoad();
			
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