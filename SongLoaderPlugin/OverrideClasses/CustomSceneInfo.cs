namespace SongLoaderPlugin
{
	public class CustomSceneInfo : SceneInfo
	{
		public void Init(GameScenesManager newGameScenesManager, string newSceneName)
		{
			_gameScenesManager = newGameScenesManager;
			_sceneName = newSceneName;
		}
	}
}