namespace SongLoaderPlugin.OverrideClasses
{
	public class CustomBeatmapDataSO : BeatmapDataSO, IScriptableObjectResetable
	{
		public string jsonData
		{
			get { return _jsonData; }
		}
		
		public void Reset()
		{
			
		}
	}
}