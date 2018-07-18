using UnityEngine;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SimpleJSON;
using SongLoaderPlugin.Internals;
using SongLoaderPlugin.OverrideClasses;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace SongLoaderPlugin
{
	public class SongLoader : MonoBehaviour
	{
		public static event Action<SongLoader> LoadingStartedEvent;
		public static event Action<SongLoader, List<CustomLevel>> SongsLoadedEvent;
		public static List<CustomLevel> CustomLevels = new List<CustomLevel>();
		public static bool AreSongsLoaded { get; private set; }
		public static float LoadingProgress { get; private set; }

		public const int MenuIndex = 1;
		
		private static readonly Dictionary<string, Sprite> LoadedSprites = new Dictionary<string, Sprite>();
		private static readonly Dictionary<string, AudioClip> LoadedAudioClips = new Dictionary<string, AudioClip>();

		private LeaderboardScoreUploader _leaderboardScoreUploader;
		private MainFlowCoordinator _mainFlowCoordinator;
		private StandardLevelDetailViewController _standardLevelDetailViewController;

		private CustomLevelCollectionsForGameplayModes _customLevelCollectionsForGameplayModes;
		private CustomLevelCollectionSO _standardLevelCollection;
		private CustomLevelCollectionSO _oneSaberLevelCollection;
		private CustomLevelCollectionSO _noArrowsLevelCollection;
		private CustomLevelCollectionSO _partyLevelCollection;
		
		private readonly ScriptableObjectPool<CustomLevel> _customLevelPool = new ScriptableObjectPool<CustomLevel>();
		private readonly ScriptableObjectPool<BeatmapDataSO> _beatmapDataPool = new ScriptableObjectPool<BeatmapDataSO>();

		private readonly AudioClip _temporaryAudioClip = AudioClip.Create("temp", 1, 2, 1000, true);
		
		public static void OnLoad()
		{
			if (Instance != null) return;
			new GameObject("Song Loader").AddComponent<SongLoader>();
		}

		public static SongLoader Instance;

		private void Awake()
		{
			Instance = this;
			CreateCustomLevelCollections();
			SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;
			SceneManagerOnActiveSceneChanged(new Scene(), SceneManager.GetActiveScene());
			ProgressBar.Create();
			
			RefreshSongs();

			DontDestroyOnLoad(gameObject);
		}

		private void SceneManagerOnActiveSceneChanged(Scene arg0, Scene scene)
		{
			StartCoroutine(WaitRemoveScores());

			if (scene.buildIndex == 1)
			{	
				_mainFlowCoordinator = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().FirstOrDefault();
				_mainFlowCoordinator.SetPrivateField("_levelCollectionsForGameplayModes", _customLevelCollectionsForGameplayModes);
				
				_standardLevelDetailViewController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().FirstOrDefault();
				if (_standardLevelDetailViewController == null) return;
				_standardLevelDetailViewController.didPressPlayButtonEvent += StandardLevelDetailControllerOnDidPressPlayButtonEvent;
				
				var standardLevelListViewController = Resources.FindObjectsOfTypeAll<StandardLevelListViewController>().FirstOrDefault();
				if (standardLevelListViewController == null) return;
				
				standardLevelListViewController.didSelectLevelEvent += StandardLevelListViewControllerOnDidSelectLevelEvent;
			}
			else if (scene.buildIndex == 5)
			{
				if (NoteHitVolumeChanger.PrefabFound) return;
				var mainGameData = Resources.FindObjectsOfTypeAll<MainGameSceneSetupData>().FirstOrDefault();
				if (mainGameData == null) return;
				var level = mainGameData.difficultyLevel.level;
				var song = CustomLevels.FirstOrDefault(x => x.levelID == level.levelID);
				if (song == null) return;
				NoteHitVolumeChanger.SetVolume(song.customSongInfo.noteHitVolume, song.customSongInfo.noteMissVolume);
			}
		}

		private void StandardLevelListViewControllerOnDidSelectLevelEvent(StandardLevelListViewController arg1, IStandardLevel level)
		{
			var customLevel = level as CustomLevel;
			if (customLevel == null) return;

			if (customLevel.audioClip.name != "temp" || customLevel.AudioClipLoading) return;

			var levels = arg1.GetPrivateField<IStandardLevel[]>("_levels").ToList();
			
			Action callback = delegate
			{
				arg1.SetPrivateField("_selectedLevel", null);
				arg1.HandleLevelSelectionDidChange(levels.IndexOf(customLevel), true);
			};

			StartCoroutine(LoadAudio(
				"file:///" + customLevel.customSongInfo.path + "/" + customLevel.customSongInfo.GetAudioPath(), customLevel,
				callback));
		}

		private IEnumerator WaitRemoveScores()
		{
			yield return new WaitForSecondsRealtime(1f);
			RemoveCustomScores();
		}

		private void StandardLevelDetailControllerOnDidPressPlayButtonEvent(StandardLevelDetailViewController songDetailViewController)
		{
			if (!NoteHitVolumeChanger.PrefabFound) return;
			var level = songDetailViewController.difficultyLevel.level;
			var song = CustomLevels.FirstOrDefault(x => x.levelID == level.levelID);
			if (song == null) return;
			NoteHitVolumeChanger.SetVolume(song.customSongInfo.noteHitVolume, song.customSongInfo.noteMissVolume);
		}

		private void CreateCustomLevelCollections()
		{
			var originalCollections = Resources.FindObjectsOfTypeAll<LevelCollectionsForGameplayModes>().FirstOrDefault();
			
			_standardLevelCollection = ScriptableObject.CreateInstance<CustomLevelCollectionSO>();
			_standardLevelCollection.Init(originalCollections.GetLevels(GameplayMode.SoloStandard));

			_oneSaberLevelCollection = ScriptableObject.CreateInstance<CustomLevelCollectionSO>();
			_oneSaberLevelCollection.Init(originalCollections.GetLevels(GameplayMode.SoloOneSaber));
			
			_noArrowsLevelCollection = ScriptableObject.CreateInstance<CustomLevelCollectionSO>();
			_noArrowsLevelCollection.Init(originalCollections.GetLevels(GameplayMode.SoloNoArrows));
			
			_partyLevelCollection = ScriptableObject.CreateInstance<CustomLevelCollectionSO>();
			_partyLevelCollection.Init(originalCollections.GetLevels(GameplayMode.PartyStandard));

			_customLevelCollectionsForGameplayModes =
				ScriptableObject.CreateInstance<CustomLevelCollectionsForGameplayModes>();

			var standard = new CustomLevelCollectionForGameplayMode(GameplayMode.SoloStandard, _standardLevelCollection);
			var oneSaber = new CustomLevelCollectionForGameplayMode(GameplayMode.SoloOneSaber, _oneSaberLevelCollection);
			var noArrows = new CustomLevelCollectionForGameplayMode(GameplayMode.SoloNoArrows, _noArrowsLevelCollection);
			var party = new CustomLevelCollectionForGameplayMode(GameplayMode.PartyStandard, _partyLevelCollection);

			_customLevelCollectionsForGameplayModes.SetCollections(
				new LevelCollectionsForGameplayModes.LevelCollectionForGameplayMode[]
					{standard, oneSaber, noArrows, party});
		}

		public void RefreshSongs(bool fullRefresh = true)
		{
			if (SceneManager.GetActiveScene().buildIndex != MenuIndex) return;
			Log(fullRefresh ? "Starting full song refresh" : "Starting song refresh");
			AreSongsLoaded = false;
			LoadingProgress = 0;
			
			if (LoadingStartedEvent != null)
			{
				LoadingStartedEvent(this);
			}

			foreach (var customLevel in CustomLevels)
			{
				_standardLevelCollection.LevelList.Remove(customLevel);
				_oneSaberLevelCollection.LevelList.Remove(customLevel);
				_noArrowsLevelCollection.LevelList.Remove(customLevel);
				_partyLevelCollection.LevelList.Remove(customLevel);
			}

			
			RetrieveAllSongs(fullRefresh);
		}

		//Use these methods if your own plugin deletes a song and you want the song loader to remove it from the list.
		//This is so you don't have to do a full refresh.
		public void RemoveSongWithPath(string path)
		{
			RemoveSong(CustomLevels.FirstOrDefault(x => x.customSongInfo.path == path));
		}

		public void RemoveSongWithLevelID(string levelID)
		{
			RemoveSong(CustomLevels.FirstOrDefault(x => x.levelID == levelID));
		}

		public void RemoveSong(IStandardLevel level)
		{
			if (level == null) return;
			RemoveSong(level as CustomLevel);
		}

		public void RemoveSong(CustomLevel customLevel)
		{
			if (customLevel == null) return;
			
			_standardLevelCollection.LevelList.Remove(customLevel);
			_oneSaberLevelCollection.LevelList.Remove(customLevel);
			_noArrowsLevelCollection.LevelList.Remove(customLevel);
			_partyLevelCollection.LevelList.Remove(customLevel);

			foreach (var difficultyBeatmap in customLevel.difficultyBeatmaps)
			{
				var customDifficulty = difficultyBeatmap as CustomLevel.CustomDifficultyBeatmap;
				if (customDifficulty == null) continue;
				_beatmapDataPool.Return(customDifficulty.BeatmapDataSO);
			}
			
			_customLevelPool.Return(customLevel);
		}

		private void RemoveCustomScores()
		{
			if (PlayerPrefs.HasKey("lbPatched")) return;
			_leaderboardScoreUploader = FindObjectOfType<LeaderboardScoreUploader>();
			if (_leaderboardScoreUploader == null) return;
			var scores =
				_leaderboardScoreUploader.GetPrivateField<List<LeaderboardScoreUploader.ScoreData>>("_scoresToUploadForCurrentPlayer");

			var scoresToRemove = new List<LeaderboardScoreUploader.ScoreData>();
			foreach (var scoreData in scores)
			{
				var split = scoreData._leaderboardId.Split('_');
				var levelID = split[0];
				if (CustomLevels.Any(x => x.levelID == levelID))
				{
					Log("Removing a custom score here");
					scoresToRemove.Add(scoreData);
				}
			}

			scores.RemoveAll(x => scoresToRemove.Contains(x));
		}

		private IEnumerator LoadSprite(string spritePath, CustomLevel customLevel)
		{
			Sprite sprite;
			if (!LoadedSprites.ContainsKey(spritePath))
			{
				using (var web = UnityWebRequestTexture.GetTexture(EncodePath(spritePath), true))
				{
					yield return web.SendWebRequest();
					if (web.isNetworkError || web.isHttpError)
					{
						Log("Error loading: " + spritePath + ": " + web.error);
						sprite = null;
					}
					else
					{
						var tex = DownloadHandlerTexture.GetContent(web);
						sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, 100, 1);
						LoadedSprites.Add(spritePath, sprite);
					}
				}
			}
			else
			{
				sprite = LoadedSprites[spritePath];
			}

			customLevel.SetCoverImage(sprite);
		}
		
		private IEnumerator LoadAudio(string audioPath, CustomLevel customLevel, Action callback)
		{
			AudioClip audioClip;
			if (!LoadedAudioClips.ContainsKey(audioPath))
			{
				using (var www = new WWW(EncodePath(audioPath)))
				{
					customLevel.AudioClipLoading = true;
					yield return www;
					
					audioClip = www.GetAudioClip(true, true, AudioType.UNKNOWN);

					var timeout = Time.realtimeSinceStartup + 5;
					while (audioClip.length == 0)
					{
						if (Time.realtimeSinceStartup > timeout)
						{
							Log("Audio clip: " + audioClip.name + " timed out...");
							break;
						}

						yield return null;
					}
					
					LoadedAudioClips.Add(audioPath, audioClip);
				}
			}
			else
			{
				audioClip = LoadedAudioClips[audioPath];
			}

			customLevel.SetAudioClip(audioClip);
			callback.Invoke();
			customLevel.AudioClipLoading = false;
		}

		private void RetrieveAllSongs(bool fullRefresh)
		{
			var stopwatch = new Stopwatch();
			var levelList = new List<CustomLevel>();

			if (fullRefresh)
			{
				_customLevelPool.ReturnAll();
				_beatmapDataPool.ReturnAll();				
				CustomLevels.Clear();
			}
			
			Action job = delegate
			{
				try
				{	
					stopwatch.Start();
					var path = Environment.CurrentDirectory;
					path = path.Replace('\\', '/');

					var currentHashes = new List<string>();
					var cachedSongs = new string[0];
					if (Directory.Exists(path + "/CustomSongs/.cache"))
					{
						cachedSongs = Directory.GetDirectories(path + "/CustomSongs/.cache");
					}
					else
					{
						Directory.CreateDirectory(path + "/CustomSongs/.cache");
					}

					var songZips = Directory.GetFiles(path + "/CustomSongs")
						.Where(x => x.ToLower().EndsWith(".zip") || x.ToLower().EndsWith(".beat")).ToArray();
					foreach (var songZip in songZips)
					{
						Log("Found zip: " + songZip);
						//Check cache if zip already is extracted
						string hash;
						if (Utils.CreateMD5FromFile(songZip, out hash))
						{
							currentHashes.Add(hash);
							if (cachedSongs.Any(x => x.Contains(hash))) continue;

							using (var unzip = new Unzip(songZip))
							{
								unzip.ExtractToDirectory(path + "/CustomSongs/.cache/" + hash);
								Log("Extracted to " + path + "/CustomSongs/.cache/" + hash);
							}
						}
						else
						{
							Log("Error reading zip " + songZip);
						}
					}

					var songFolders = Directory.GetDirectories(path + "/CustomSongs").ToList();
					var songCaches = Directory.GetDirectories(path + "/CustomSongs/.cache");
					
					float i = 0;
					foreach (var song in songFolders)
					{
						i++;
						var results = Directory.GetFiles(song, "info.json", SearchOption.AllDirectories);
						if (results.Length == 0)
						{
							Log("Custom song folder '" + song + "' is missing info.json!");
							continue;
						}

						
						foreach (var result in results)
						{
							var songPath = Path.GetDirectoryName(result).Replace('\\', '/');
							if (!fullRefresh)
							{
								if (CustomLevels.Any(x => x.customSongInfo.path == songPath))
								{
									continue;
								}
							}
							
							var customSongInfo = GetCustomSongInfo(songPath);
							if (customSongInfo == null) continue;
							var id = customSongInfo.GetIdentifier();
							if (CustomLevels.Any(x => x.levelID == id && x.customSongInfo != customSongInfo))
							{
								Log("Duplicate song found at " + customSongInfo.path);
								continue;
							}

							var i1 = i;
							HMMainThreadDispatcher.instance.Enqueue(delegate
							{
								LoadSong(customSongInfo, levelList);
								LoadingProgress = i1 / songFolders.Count;
							});
						}
					}

					foreach (var song in songCaches)
					{
						var hash = Path.GetFileName(song);
						if (!currentHashes.Contains(hash))
						{
							//Old cache
							Log("Deleting old cache: " + song);
							Directory.Delete(song, true);
						}
					}

				}
				catch (Exception e)
				{
					Log("RetrieveAllSongs failed:");
					Log(e.ToString());
					throw;
				}
			};
			
			Action finish = delegate
			{
				stopwatch.Stop();
				Log("Loaded " + levelList.Count + " new songs in " + stopwatch.Elapsed.Seconds + " seconds");
				
				CustomLevels.AddRange(levelList);
				var orderedList = CustomLevels.OrderBy(x => x.songName);
				CustomLevels = orderedList.ToList();

				foreach (var customLevel in CustomLevels)
				{	
					if (customLevel.customSongInfo.oneSaber)
					{
						_oneSaberLevelCollection.LevelList.Add(customLevel);
					}
					else
					{
						_standardLevelCollection.LevelList.Add(customLevel);
						_noArrowsLevelCollection.LevelList.Add(customLevel);
						_partyLevelCollection.LevelList.Add(customLevel);
					}
				}

				AreSongsLoaded = true;
				LoadingProgress = 1;

				if (SongsLoadedEvent != null)
				{
					SongsLoadedEvent(this, CustomLevels);
				}
			};
			
			var task = new HMTask(job, finish);
			task.Run();
		}

		private void LoadSong(CustomSongInfo song, List<CustomLevel> levelList)
		{
			try
			{
				var newLevel = _customLevelPool.Get();
				newLevel.Init(song);
				newLevel.SetAudioClip(_temporaryAudioClip);

				var difficultyBeatmaps = new List<StandardLevelSO.DifficultyBeatmap>();
				foreach (var diffBeatmap in song.difficultyLevels)
				{
					try
					{
						var difficulty = diffBeatmap.difficulty.ToEnum(LevelDifficulty.Normal);

						if (string.IsNullOrEmpty(diffBeatmap.json))
						{
							Log("Couldn't find or parse difficulty json " + song.path + "/" + diffBeatmap.jsonPath);
							continue;
						}

						var newBeatmapData = _beatmapDataPool.Get();
						newBeatmapData.SetJsonData(diffBeatmap.json);

						var newDiffBeatmap = new CustomLevel.CustomDifficultyBeatmap(newLevel, difficulty,
							diffBeatmap.difficultyRank, newBeatmapData);
						difficultyBeatmaps.Add(newDiffBeatmap);
					}
					catch (Exception e)
					{
						Log("Error parsing difficulty level in song: " + song.path);
						Log(e.Message);
					}
				}

				if (difficultyBeatmaps.Count == 0) return;

				newLevel.SetDifficultyBeatmaps(difficultyBeatmaps.ToArray());
				newLevel.InitData();

				StartCoroutine(LoadSprite("file:///" + song.path + "/" + song.coverImagePath, newLevel));
				levelList.Add(newLevel);
			}
			catch (Exception e)
			{
				Log("Failed to load song: " + song.path);
				Log(e.ToString());
			}
		}

		private CustomSongInfo GetCustomSongInfo(string songPath)
		{
			var infoText = File.ReadAllText(songPath + "/info.json");
			CustomSongInfo songInfo;
			try
			{
				songInfo = JsonUtility.FromJson<CustomSongInfo>(infoText);
			}
			catch (Exception)
			{
				Log("Error parsing song: " + songPath);
				return null;
			}

			songInfo.path = songPath;

			//Here comes SimpleJSON to the rescue when JSONUtility can't handle an array.
			var diffLevels = new List<CustomSongInfo.DifficultyLevel>();
			var n = JSON.Parse(infoText);
			var diffs = n["difficultyLevels"];
			for (int i = 0; i < diffs.AsArray.Count; i++)
			{
				n = diffs[i];
				diffLevels.Add(new CustomSongInfo.DifficultyLevel
				{
					difficulty = n["difficulty"],
					difficultyRank = n["difficultyRank"].AsInt,
					audioPath = n["audioPath"],
					jsonPath = n["jsonPath"]
				});
			}

			songInfo.difficultyLevels = diffLevels.ToArray();
			return songInfo;
		}

		private void Log(string message)
		{
			//Debug.Log("Song Loader: " + message);
			Console.WriteLine("Song Loader: " + message);
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.R))
			{
				RefreshSongs(Input.GetKey(KeyCode.LeftControl));
			}
		}

		private static string EncodePath(string path)
		{
			path = Uri.EscapeDataString(path);
			path = path.Replace("%2F", "/"); //Forward slash gets encoded, but it shouldn't.
			path = path.Replace("%3A", ":"); //Same with semicolon.
			return path;
		}
	}
}