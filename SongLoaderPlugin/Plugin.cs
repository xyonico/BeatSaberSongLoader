using IllusionPlugin;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SongLoaderPlugin
{
	public class Plugin : IPlugin
	{	
		public string Name
		{
			get { return "Song Loader Plugin"; }
		}

		public string Version
		{
			get { return "v4.3.0"; }
		}
		
		public void OnApplicationStart()
		{
			SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;
		}

		public void OnApplicationQuit()
		{
			PlayerPrefs.DeleteKey("lbPatched");
			SceneManager.activeSceneChanged -= SceneManagerOnActiveSceneChanged;
		}

		private void SceneManagerOnActiveSceneChanged(Scene oldScene, Scene newScene)
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