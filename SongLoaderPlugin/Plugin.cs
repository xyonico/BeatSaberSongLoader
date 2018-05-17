using System;
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
			get { return "v3.1"; }
		}
		
		public void OnApplicationStart()
		{
			
		}

		public void OnApplicationQuit()
		{
			PlayerPrefs.DeleteKey("lbPatched");
		}

		public void OnUpdate()
		{
			
		}

		public void OnFixedUpdate()
		{
			
		}

		public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode) {
			if (scene.buildIndex != SongLoader.MenuIndex) return;
			SongLoader.OnLoad();
		}
		
		public void OnSceneUnloaded(Scene scene) {
			
		}

		public void OnActiveSceneChanged(Scene prevScene, Scene nextScene) {
			
		}
	}
}