using System;
using IllusionPlugin;
using UnityEngine;

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
			get { return "0.0.2"; }
		}
		
		public void OnApplicationStart()
		{
			
		}

		public void OnApplicationQuit()
		{
			PlayerPrefs.SetInt("lbPatched", 0);
		}

		public void OnLevelWasLoaded(int level)
		{
			
		}

		public void OnLevelWasInitialized(int level)
		{
			if (level != SongLoader.MenuIndex) return;
			SongLoader.OnLoad();
		}

		public void OnUpdate()
		{
			
		}

		public void OnFixedUpdate()
		{
			
		}
	}
}