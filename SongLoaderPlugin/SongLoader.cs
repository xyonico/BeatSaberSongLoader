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
		public static bool AreSongsLoading { get; private set; }
		public static float LoadingProgress { get; private set; }

		public const string MenuSceneName = "Menu";
		public const string GameSceneName = "StandardLevel";
		
		private static readonly Dictionary<string, Sprite> LoadedSprites = new Dictionary<string, Sprite>();
		private static readonly Dictionary<string, AudioClip> LoadedAudioClips = new Dictionary<string, AudioClip>();

		private LeaderboardScoreUploader _leaderboardScoreUploader;
		private MainFlowCoordinator _mainFlowCoordinator;
		private StandardLevelDetailViewController _standardLevelDetailViewController;
		private MainGameSceneSetupData _mainGameSceneSetupData;

		private CustomLevelCollectionsForGameplayModes _customLevelCollectionsForGameplayModes;
		private CustomLevelCollectionSO _standardLevelCollection;
		private CustomLevelCollectionSO _oneSaberLevelCollection;
		private CustomLevelCollectionSO _noArrowsLevelCollection;
		private CustomLevelCollectionSO _partyLevelCollection;
		
		private readonly ScriptableObjectPool<CustomLevel> _customLevelPool = new ScriptableObjectPool<CustomLevel>();
		private readonly ScriptableObjectPool<CustomBeatmapDataSO> _beatmapDataPool = new ScriptableObjectPool<CustomBeatmapDataSO>();

		private ProgressBar _progressBar;

		private HMTask _loadingTask;
		private bool _loadingCancelled;

		private CustomLevel.CustomDifficultyBeatmap _currentLevelPlaying;

		public static readonly AudioClip TemporaryAudioClip = AudioClip.Create("temp", 1, 2, 1000, true);

		private LogSeverity _minLogSeverity;
		
		public static void OnLoad()
		{
			if (Instance != null) return;
			new GameObject("Song Loader").AddComponent<SongLoader>();
		}

		public static SongLoader Instance;

		private void Awake()
		{
			Instance = this;
			
			_minLogSeverity = Environment.CommandLine.Contains("--mute-song-loader")
				? LogSeverity.Error
				: LogSeverity.Info;
			
			CreateCustomLevelCollections();
			SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;
			SceneManagerOnActiveSceneChanged(new Scene(), SceneManager.GetActiveScene());
			_progressBar = ProgressBar.Create();
			
			RefreshSongs();

			DontDestroyOnLoad(gameObject);
		}

		private void SceneManagerOnActiveSceneChanged(Scene arg0, Scene scene)
		{
			if (AreSongsLoading)
			{
				//Scene changing while songs are loading. Since we are using a separate thread while loading, this is bad and could cause a crash.
				//So we have to stop loading.
				if (_loadingTask != null)
				{
					_loadingTask.Cancel();
					_loadingCancelled = true;
					AreSongsLoading = false;
					LoadingProgress = 0;
					StopAllCoroutines();
					_progressBar.ShowMessage("Loading cancelled\n<size=80%>Press Ctrl+R to refresh</size>");
					Log("Loading was cancelled by player since they loaded another scene.");
				}
			}
			
			StartCoroutine(WaitRemoveScores());

			if (scene.name == MenuSceneName)
			{
				_currentLevelPlaying = null;
				_mainFlowCoordinator = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().FirstOrDefault();
				_mainFlowCoordinator.SetPrivateField("_levelCollectionsForGameplayModes", _customLevelCollectionsForGameplayModes);
				
				_standardLevelDetailViewController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().FirstOrDefault();
				if (_standardLevelDetailViewController == null) return;
				_standardLevelDetailViewController.didPressPlayButtonEvent += StandardLevelDetailControllerOnDidPressPlayButtonEvent;
				
				var standardLevelListViewController = Resources.FindObjectsOfTypeAll<StandardLevelListViewController>().FirstOrDefault();
				if (standardLevelListViewController == null) return;
				
				standardLevelListViewController.didSelectLevelEvent += StandardLevelListViewControllerOnDidSelectLevelEvent;
			}
			else if (scene.name == GameSceneName)
			{
				_mainGameSceneSetupData = Resources.FindObjectsOfTypeAll<MainGameSceneSetupData>().FirstOrDefault();
				if (_mainGameSceneSetupData == null) return;
				var level = _mainGameSceneSetupData.difficultyLevel;
				var beatmap = level as CustomLevel.CustomDifficultyBeatmap;
				if (beatmap != null)
				{
					_currentLevelPlaying = beatmap;
					
					//Beat Saber 0.11.1 introduced a check for if noteJumpMovementSpeed <= 0
					//This breaks songs that have a negative noteJumpMovementSpeed and previously required a patcher to get working again
					//I've added this to add support for that again, because why not.
					if (_currentLevelPlaying.noteJumpMovementSpeed <= 0)
					{
						var beatmapObjectSpawnController =
							Resources.FindObjectsOfTypeAll<BeatmapObjectSpawnController>().FirstOrDefault();
						if (beatmapObjectSpawnController != null)
						{
							beatmapObjectSpawnController.Init(_currentLevelPlaying.level.beatsPerMinute,
								_currentLevelPlaying.beatmapData.beatmapLinesData.Length,
								_currentLevelPlaying.noteJumpMovementSpeed);
						}
					}
				}

				
				if (NoteHitVolumeChanger.PrefabFound) return;
				var song = CustomLevels.FirstOrDefault(x => x.levelID == level.level.levelID);
				if (song == null) return;
				NoteHitVolumeChanger.SetVolume(song.customSongInfo.noteHitVolume, song.customSongInfo.noteMissVolume);
			}
		}

		private void StandardLevelListViewControllerOnDidSelectLevelEvent(StandardLevelListViewController arg1, IStandardLevel level)
		{
			var customLevel = level as CustomLevel;
			if (customLevel == null) return;

			if (customLevel.audioClip != TemporaryAudioClip || customLevel.AudioClipLoading) return;

			var levels = arg1.GetPrivateField<IStandardLevel[]>("_levels").ToList();
			
			Action callback = delegate
			{
				arg1.SetPrivateField("_selectedLevel", null);
				arg1.HandleLevelSelectionDidChange(levels.IndexOf(customLevel), true);
			};

			customLevel.FixBPMAndGetNoteJumpMovementSpeed();
			StartCoroutine(LoadAudio(
				"file:///" + customLevel.customSongInfo.path + "/" + customLevel.customSongInfo.GetAudioPath(), customLevel,
				callback));
		}

		public void LoadAudioClipForLevel(CustomLevel customLevel, Action<CustomLevel> clipReadyCallback)
		{
			Action callback = delegate { clipReadyCallback(customLevel); };
			
			customLevel.FixBPMAndGetNoteJumpMovementSpeed();
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
			if (SceneManager.GetActiveScene().name != MenuSceneName) return;
			if (AreSongsLoading) return;
			
			Log(fullRefresh ? "Starting full song refresh" : "Starting song refresh");
			AreSongsLoaded = false;
			AreSongsLoading = true;
			LoadingProgress = 0;
			_loadingCancelled = false;

			if (LoadingStartedEvent != null)
			{
				try
				{
					LoadingStartedEvent(this);
				}
				catch (Exception e)
				{
					Log("Some plugin is throwing exception from the LoadingStartedEvent!", LogSeverity.Error);
					Log(e.ToString(), LogSeverity.Error);
				}
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
						Log("Error loading: " + spritePath + ": " + web.error, LogSeverity.Warn);
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
							Log("Audio clip: " + audioClip.name + " timed out...", LogSeverity.Warn);
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
					
					var songZips = Directory.GetFiles(path + "/CustomSongs")
						.Where(x => x.ToLower().EndsWith(".zip") || x.ToLower().EndsWith(".beat")).ToArray();
					foreach (var songZip in songZips)
					{
						//Check cache if zip already is extracted
						string hash;
						if (Utils.CreateMD5FromFile(songZip, out hash))
						{
							currentHashes.Add(hash);
							if (cachedSongs.Any(x => x.Contains(hash))) continue;

							using (var unzip = new Unzip(songZip))
							{
								try
								{
									unzip.ExtractToDirectory(path + "/CustomSongs/.cache/" + hash);
								}
								catch (Exception e)
								{
									Log("Error extracting zip " + songZip + "\n" + e, LogSeverity.Warn);
								}
							}
						}
						else
						{
							Log("Error reading zip " + songZip, LogSeverity.Warn);
						}
					}

					var songFolders = Directory.GetDirectories(path + "/CustomSongs").ToList();
					var songCaches = Directory.GetDirectories(path + "/CustomSongs/.cache");
					
					var loadedIDs = new List<string>();
					
					float i = 0;
					foreach (var song in songFolders)
					{
						i++;
						var results = Directory.GetFiles(song, "info.json", SearchOption.AllDirectories);
						if (results.Length == 0)
						{
							Log("Custom song folder '" + song + "' is missing info.json files!", LogSeverity.Warn);
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
							if (loadedIDs.Any(x => x == id))
							{
								Log("Duplicate song found at " + customSongInfo.path, LogSeverity.Warn);
								continue;
							}
							
							loadedIDs.Add(id);

							var i1 = i;
							HMMainThreadDispatcher.instance.Enqueue(delegate
							{
								if (_loadingCancelled) return;
								var level = LoadSong(customSongInfo);
								if (level != null)
								{
									levelList.Add(level);
								}
								
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
							Directory.Delete(song, true);
						}
					}

				}
				catch (Exception e)
				{
					Log("RetrieveAllSongs failed:", LogSeverity.Error);
					Log(e.ToString(), LogSeverity.Error);
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
				AreSongsLoading = false;
				LoadingProgress = 1;

				_loadingTask = null;
				
				if (SongsLoadedEvent != null)
				{
					SongsLoadedEvent(this, CustomLevels);
				}
			};
			
			_loadingTask = new HMTask(job, finish);
			_loadingTask.Run();
		}

		private CustomLevel LoadSong(CustomSongInfo song)
		{
			try
			{
				var newLevel = _customLevelPool.Get();
				newLevel.Init(song);
				newLevel.SetAudioClip(TemporaryAudioClip);

				var difficultyBeatmaps = new List<StandardLevelSO.DifficultyBeatmap>();
				foreach (var diffBeatmap in song.difficultyLevels)
				{
					try
					{
						var difficulty = diffBeatmap.difficulty.ToEnum(LevelDifficulty.Normal);

						if (string.IsNullOrEmpty(diffBeatmap.json))
						{
							Log("Couldn't find or parse difficulty json " + song.path + "/" + diffBeatmap.jsonPath, LogSeverity.Warn);
							continue;
						}

						var newBeatmapData = _beatmapDataPool.Get();
						newBeatmapData.SetJsonData(diffBeatmap.json);

						var newDiffBeatmap = new CustomLevel.CustomDifficultyBeatmap(newLevel, difficulty,
							diffBeatmap.difficultyRank, diffBeatmap.noteJumpMovementSpeed, newBeatmapData);
						difficultyBeatmaps.Add(newDiffBeatmap);
					}
					catch (Exception e)
					{
						Log("Error parsing difficulty level in song: " + song.path, LogSeverity.Warn);
						Log(e.Message, LogSeverity.Warn);
					}
				}

				if (difficultyBeatmaps.Count == 0) return null;

				newLevel.SetDifficultyBeatmaps(difficultyBeatmaps.ToArray());
				newLevel.InitData();

				StartCoroutine(LoadSprite("file:///" + song.path + "/" + song.coverImagePath, newLevel));
				return newLevel;
			}
			catch (Exception e)
			{
				Log("Failed to load song: " + song.path, LogSeverity.Warn);
				Log(e.ToString(), LogSeverity.Warn);
			}

			return null;
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
				Log("Error parsing song: " + songPath, LogSeverity.Warn);
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
				var difficulty = Utils.ToEnum(n["difficulty"], LevelDifficulty.Normal);
				var difficultyRank = (int)difficulty;
				
				diffLevels.Add(new CustomSongInfo.DifficultyLevel
				{
					difficulty = n["difficulty"],
					difficultyRank = difficultyRank,
					audioPath = n["audioPath"],
					jsonPath = n["jsonPath"],
					noteJumpMovementSpeed = n["noteJumpMovementSpeed"]
				});
			}

			songInfo.difficultyLevels = diffLevels.ToArray();
			return songInfo;
		}

		private void Log(string message, LogSeverity severity = LogSeverity.Info)
		{
			if (severity < _minLogSeverity) return;
			Console.WriteLine("Song Loader [" + severity.ToString().ToUpper() + "]: " + message);
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.R))
			{
				if (_currentLevelPlaying != null)
				{
					ReloadCurrentSong();
					return;
				}
				RefreshSongs(Input.GetKey(KeyCode.LeftControl));
			}
		}

		private void ReloadCurrentSong()
		{
			if (!_mainGameSceneSetupData.gameplayOptions.noEnergy) return;
			var reloadedLevel = LoadSong(GetCustomSongInfo(_currentLevelPlaying.customLevel.customSongInfo.path));
			if (reloadedLevel == null) return;
			
			reloadedLevel.FixBPMAndGetNoteJumpMovementSpeed();
			reloadedLevel.SetAudioClip(_currentLevelPlaying.customLevel.audioClip);
					
			RemoveSong(_currentLevelPlaying.customLevel);
			CustomLevels.Add(reloadedLevel);
					
			if (reloadedLevel.customSongInfo.oneSaber)
			{
				_oneSaberLevelCollection.LevelList.Add(reloadedLevel);
			}
			else
			{
				_standardLevelCollection.LevelList.Add(reloadedLevel);
				_noArrowsLevelCollection.LevelList.Add(reloadedLevel);
				_partyLevelCollection.LevelList.Add(reloadedLevel);
			}
					
			var orderedList = CustomLevels.OrderBy(x => x.songName);
			CustomLevels = orderedList.ToList();
					
			_mainGameSceneSetupData.WillBeUsedInTransition();
			_mainGameSceneSetupData.Init(
				reloadedLevel.GetDifficultyLevel(_mainGameSceneSetupData.difficultyLevel.difficulty),
				_mainGameSceneSetupData.gameplayOptions, _mainGameSceneSetupData.gameplayMode, 0);
			_mainGameSceneSetupData.TransitionToScene(0.35f);
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