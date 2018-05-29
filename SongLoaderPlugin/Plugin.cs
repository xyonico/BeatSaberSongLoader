using System;
using IllusionPlugin;
using SongLoaderPlugin.Parallel;
using UnityEngine;

namespace SongLoaderPlugin
{
	public class Plugin : IPlugin
	{
		// CHANGE: Identified as parallel fork
		public string Name
		{
			get { return "Song Loader Plugin, Parallel fork"; }
		}

		// CHANGE: Identified as parallel version
		public string Version
		{
			get { return "v3.1 P"; }
		}
		
		public void OnApplicationStart()
		{
			
		}

		public void OnApplicationQuit()
		{
			PlayerPrefs.DeleteKey("lbPatched");
		}

		public void OnLevelWasLoaded(int level)
		{
			
		}

		public void OnLevelWasInitialized(int level)
		{
			if (level != SongLoader.MenuIndex) return;
			// CHANGE
			// SongLoader.OnLoad();
			AsyncSongLoader.OnLoad();
		}

		public void OnUpdate()
		{
			
		}

		public void OnFixedUpdate()
		{
			
		}
	}
}