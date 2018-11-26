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
		public static CustomLevelCollectionSO CustomLevelCollectionSO { get; private set; }

		public const string MenuSceneName = "Menu";
		public const string GameSceneName = "GameCore";
		
		private static readonly Dictionary<string, Sprite> LoadedSprites = new Dictionary<string, Sprite>();
		private static readonly Dictionary<string, AudioClip> LoadedAudioClips = new Dictionary<string, AudioClip>();
		
		private LeaderboardScoreUploader _leaderboardScoreUploader;
		private StandardLevelDetailViewController _standardLevelDetailViewController;
		private StandardLevelSceneSetupDataSO _standardLevelSceneSetupData;
		
		private readonly ScriptableObjectPool<CustomLevel> _customLevelPool = new ScriptableObjectPool<CustomLevel>();
		private readonly ScriptableObjectPool<CustomBeatmapDataSO> _beatmapDataPool = new ScriptableObjectPool<CustomBeatmapDataSO>();

		private ProgressBar _progressBar;

		private HMTask _loadingTask;
		private bool _loadingCancelled;
		private SceneEvents _sceneEvents;

		private CustomLevel.CustomDifficultyBeatmap _currentLevelPlaying;

		public static readonly AudioClip TemporaryAudioClip = AudioClip.Create("temp", 1, 2, 1000, true);

		private LogSeverity _minLogSeverity;
		private bool _noArrowsSelected;
		
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
			
			_progressBar = ProgressBar.Create();
			
			OnSceneTransitioned(SceneManager.GetActiveScene());
			RefreshSongs();

			DontDestroyOnLoad(gameObject);

			SceneEvents.Instance.SceneTransitioned += OnSceneTransitioned;
		}

		private void OnSceneTransitioned(Scene activeScene)
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
			
			if (activeScene.name == MenuSceneName)
			{
				_currentLevelPlaying = null;

				if (CustomLevelCollectionSO == null)
				{
					var levelCollectionSO = Resources.FindObjectsOfTypeAll<LevelCollectionSO>().FirstOrDefault();
					CustomLevelCollectionSO = CustomLevelCollectionSO.ReplaceOriginal(levelCollectionSO);
				}
				else
				{
					CustomLevelCollectionSO.ReplaceReferences();
				}
				
				_standardLevelDetailViewController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().FirstOrDefault();
				if (_standardLevelDetailViewController == null) return;
				_standardLevelDetailViewController.didPressPlayButtonEvent += StandardLevelDetailControllerOnDidPressPlayButtonEvent;
				
				var levelListViewController = Resources.FindObjectsOfTypeAll<LevelListViewController>().FirstOrDefault();
				if (levelListViewController == null) return;
				
				levelListViewController.didSelectLevelEvent += StandardLevelListViewControllerOnDidSelectLevelEvent;

				var characteristicViewController = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSelectionViewController>().FirstOrDefault();
				if (characteristicViewController == null) return;
				
				characteristicViewController.didSelectBeatmapCharacteristicEvent += OnDidSelectBeatmapCharacteristicEvent;
			}
			else if (activeScene.name == GameSceneName)
			{
				_standardLevelSceneSetupData = Resources.FindObjectsOfTypeAll<StandardLevelSceneSetupDataSO>().FirstOrDefault();
				if (_standardLevelSceneSetupData == null) return;
				var level = _standardLevelSceneSetupData.difficultyBeatmap;
				var beatmap = level as CustomLevel.CustomDifficultyBeatmap;
				if (beatmap != null)
				{
					_currentLevelPlaying = beatmap;
					
					//The note jump movement speed now gets set in the Start method, so we're too early here. We have to wait a bit before overriding.
					Invoke(nameof(DelayedNoteJumpMovementSpeedFix), 0.1f);
				}
				
				if (NoteHitVolumeChanger.PrefabFound) return;
				var song = CustomLevels.FirstOrDefault(x => x.levelID == level.level.levelID);
				if (song == null) return;
				NoteHitVolumeChanger.SetVolume(song.customSongInfo.noteHitVolume, song.customSongInfo.noteMissVolume);
			}
		}

		private void DelayedNoteJumpMovementSpeedFix()
		{
			//Beat Saber 0.11.1 introduced a check for if noteJumpMovementSpeed <= 0
			//This breaks songs that have a negative noteJumpMovementSpeed and previously required a patcher to get working again
			//I've added this to add support for that again, because why not.
			if (_currentLevelPlaying.noteJumpMovementSpeed <= 0)
			{
				var beatmapObjectSpawnController =
					Resources.FindObjectsOfTypeAll<BeatmapObjectSpawnController>().FirstOrDefault();
				if (beatmapObjectSpawnController != null)
				{
					var disappearingArrows = beatmapObjectSpawnController.GetPrivateField<bool>("_disappearingArrows");

					beatmapObjectSpawnController.Init(_currentLevelPlaying.level.beatsPerMinute,
						_currentLevelPlaying.beatmapData.beatmapLinesData.Length,
						_currentLevelPlaying.noteJumpMovementSpeed, disappearingArrows);
				}
			}
			
			//Also change beatmap to no arrow if no arrow was selected, since Beat Saber no longer does runtime conversion for that.
			if (!_noArrowsSelected) return;
			var gameplayCore = Resources.FindObjectsOfTypeAll<GameplayCoreSceneSetup>().FirstOrDefault();
			if (gameplayCore == null) return;
			Console.WriteLine("Applying no arrow transformation");
			var transformedBeatmap = BeatmapDataNoArrowsTransform.CreateTransformedData(_currentLevelPlaying.beatmapData);
			var beatmapDataModel = gameplayCore.GetPrivateField<BeatmapDataModel>("_beatmapDataModel");
			beatmapDataModel.SetPrivateField("_beatmapData", transformedBeatmap);
		}

		private void StandardLevelListViewControllerOnDidSelectLevelEvent(LevelListViewController levelListViewController, IBeatmapLevel level)
		{
			var customLevel = level as CustomLevel;
			if (customLevel == null) return;

			if (customLevel.audioClip != TemporaryAudioClip || customLevel.AudioClipLoading) return;

			var levels = levelListViewController.GetPrivateField<IBeatmapLevel[]>("_levels").ToList();
			
			Action callback = delegate
			{
				levelListViewController.SetPrivateField("_selectedLevel", null);
				levelListViewController.HandleLevelListTableViewDidSelectRow(null, levels.IndexOf(customLevel));
			};

			customLevel.FixBPMAndGetNoteJumpMovementSpeed();
			StartCoroutine(LoadAudio(
				"file:///" + customLevel.customSongInfo.path + "/" + customLevel.customSongInfo.GetAudioPath(), customLevel,
				callback));
		}

		private void OnDidSelectBeatmapCharacteristicEvent(BeatmapCharacteristicSelectionViewController viewController, BeatmapCharacteristicSO characteristic)
		{
			_noArrowsSelected = characteristic.characteristicName == "No Arrows";
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
			var level = songDetailViewController.difficultyBeatmap.level;
			var song = CustomLevels.FirstOrDefault(x => x.levelID == level.levelID);
			if (song == null) return;
			NoteHitVolumeChanger.SetVolume(song.customSongInfo.noteHitVolume, song.customSongInfo.noteMissVolume);
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
				CustomLevelCollectionSO.RemoveLevel(customLevel);
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

		public void RemoveSong(IBeatmapLevel level)
		{
			if (level == null) return;
			RemoveSong(level as CustomLevel);
		}

		public void RemoveSong(CustomLevel customLevel)
		{
			if (customLevel == null) return;

			CustomLevelCollectionSO.RemoveLevel(customLevel);

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
				if (scoreData.beatmap.level is CustomLevel)
				{
					Log("Removing a custom score here");
					scoresToRemove.Add(scoreData);
				}
			}

			scores.RemoveAll(x => scoresToRemove.Contains(x));
		}

		private void LoadSprite(string spritePath, CustomLevel customLevel)
		{
			Sprite sprite;
			if (!LoadedSprites.ContainsKey(spritePath))
			{
				if (!File.Exists(spritePath))
				{
					//Cover image doesn't exist, ignore it.
					return;
				}
				
				var bytes = File.ReadAllBytes(spritePath);
				var tex = new Texture2D(256, 256);
				if (!tex.LoadImage(bytes, true))
				{
					Log("Failed to load cover image: " + spritePath);
					return;
				}
				
				sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, 100, 1);
				LoadedSprites.Add(spritePath, sprite);
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
							try
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
							catch (Exception e)
							{
								Log("Failed to load song folder: " + result, LogSeverity.Warn);
								Log(e.ToString(), LogSeverity.Warn);
							}
						}
					}

					foreach (var song in cachedSongs)
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
					CustomLevelCollectionSO.AddCustomLevel(customLevel);
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

				var difficultyBeatmaps = new List<LevelSO.DifficultyBeatmap>();
				foreach (var diffBeatmap in song.difficultyLevels)
				{
					try
					{
						var difficulty = diffBeatmap.difficulty.ToEnum(BeatmapDifficulty.Normal);

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

				LoadSprite(song.path + "/" + song.coverImagePath, newLevel);
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
				var difficulty = Utils.ToEnum(n["difficulty"], BeatmapDifficulty.Normal);
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
			if (!_standardLevelSceneSetupData.gameplayCoreSetupData.gameplayModifiers.noFail) return;
			var reloadedLevel = LoadSong(GetCustomSongInfo(_currentLevelPlaying.customLevel.customSongInfo.path));
			if (reloadedLevel == null) return;
			
			reloadedLevel.FixBPMAndGetNoteJumpMovementSpeed();
			reloadedLevel.SetAudioClip(_currentLevelPlaying.customLevel.audioClip);
					
			RemoveSong(_currentLevelPlaying.customLevel);
			CustomLevels.Add(reloadedLevel);
			
			CustomLevelCollectionSO.AddCustomLevel(reloadedLevel);
			
			var orderedList = CustomLevels.OrderBy(x => x.songName);
			CustomLevels = orderedList.ToList();
			
			_standardLevelSceneSetupData.__WillBeUsedInTransition();
			_standardLevelSceneSetupData.Init(
				reloadedLevel.GetDifficultyBeatmap(_standardLevelSceneSetupData.difficultyBeatmap.difficulty),
				_standardLevelSceneSetupData.gameplayCoreSetupData);

			var restartController = Resources.FindObjectsOfTypeAll<StandardLevelRestartController>().FirstOrDefault();
			if (restartController == null)
			{
				Console.WriteLine("No restart controller!");
				return;
			}
			
			restartController.RestartLevel();
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