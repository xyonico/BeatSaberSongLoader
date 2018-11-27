using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SongLoaderPlugin
{
	public class SceneEvents : MonoBehaviour
	{
		public static SceneEvents Instance { get; private set; }
		
		private bool _wasEnabled;
		
		private GameScenesManager _gameScenesManager;
		private MenuSceneSetup _menuSceneSetup;

		public event Action<Scene> SceneTransitioned;
		public event Action MenuSceneEnabled;
		public event Action MenuSceneDisabled;

		private void Awake()
		{
			Instance = this;
			SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;
			DontDestroyOnLoad(gameObject);
		}

		private void OnDestroy()
		{
			SceneManager.activeSceneChanged -= SceneManagerOnActiveSceneChanged;
		}

		private void SceneManagerOnActiveSceneChanged(Scene oldScene, Scene newScene)
		{
			if (_gameScenesManager == null)
			{
				_gameScenesManager = Resources.FindObjectsOfTypeAll<GameScenesManager>().FirstOrDefault();

				if (_gameScenesManager == null) return;

				_gameScenesManager.transitionDidFinishEvent += GameScenesManagerOnTransitionDidFinishEvent;
			}
		}

		private void GameScenesManagerOnTransitionDidFinishEvent()
		{
			if (SceneTransitioned != null)
			{
				SceneTransitioned(SceneManager.GetActiveScene());
			}
			
			if (_menuSceneSetup == null)
			{
				_menuSceneSetup = Resources.FindObjectsOfTypeAll<MenuSceneSetup>().FirstOrDefault();
				if (_menuSceneSetup == null)
				{
					MenuDisabled();
					return;
				}
			}
			
			if (!_menuSceneSetup.gameObject.activeInHierarchy)
			{
				MenuDisabled();
			}
			else
			{
				MenuEnabled();
			}
		}

		private void MenuEnabled()
		{
			if (_wasEnabled) return;
			_wasEnabled = true;
			if (MenuSceneEnabled != null)
			{
				MenuSceneEnabled();
			}
		}

		private void MenuDisabled()
		{
			if (!_wasEnabled) return;
			_wasEnabled = false;
			if (MenuSceneDisabled != null)
			{
				MenuSceneDisabled();
			}
		}
	}
}