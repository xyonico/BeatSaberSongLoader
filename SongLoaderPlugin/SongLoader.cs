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
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace SongLoaderPlugin
{
	public class SongLoader : MonoBehaviour
	{
		public static readonly UnityEvent SongsLoaded = new UnityEvent();
		public static readonly List<CustomSongInfo> CustomSongInfos = new List<CustomSongInfo>();
		public static readonly List<CustomLevel> CustomLevels = new List<CustomLevel>();

		public const int MenuIndex = 1;
		
		private static readonly Dictionary<string, AudioClip> LoadedAudioClips = new Dictionary<string, AudioClip>();
		private static readonly Dictionary<string, Sprite> LoadedSprites = new Dictionary<string, Sprite>();

		private LeaderboardScoreUploader _leaderboardScoreUploader;
		private LevelCollectionsForGameplayModes _levelCollections;

		public static void OnLoad()
		{
			if (Instance != null) return;
			new GameObject("Song Loader").AddComponent<SongLoader>();
		}

		public static SongLoader Instance;

		private void Awake()
		{
			Instance = this;
			RefreshSongs();
			SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;
			SceneManagerOnActiveSceneChanged(new Scene(), new Scene());

			DontDestroyOnLoad(gameObject);
		}

		private void SceneManagerOnActiveSceneChanged(Scene arg0, Scene scene)
		{
			StartCoroutine(WaitRemoveScores());

			if (scene.buildIndex == 1)
			{
				if (!NoteHitVolumeChanger.PrefabFound) return;
				var songDetailController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().FirstOrDefault();
				if (songDetailController == null) return;
				songDetailController.didPressPlayButtonEvent += StandardLevelDetailControllerOnDidPressPlayButtonEvent;
			}
			else if (scene.buildIndex == 4)
			{
				if (NoteHitVolumeChanger.PrefabFound) return;
				var mainGameData = Resources.FindObjectsOfTypeAll<MainGameSceneSetupData>().FirstOrDefault();
				if (mainGameData == null) return;
				var level = mainGameData.difficultyLevel.level;
				var song = CustomSongInfos.FirstOrDefault(x => x.levelId == level.levelID);
				if (song == null) return;
				NoteHitVolumeChanger.SetVolume(song.noteHitVolume, song.noteMissVolume);
			}
		}

		private IEnumerator WaitRemoveScores()
		{
			yield return new WaitForSecondsRealtime(1f);
			RemoveCustomScores();
		}

		private void StandardLevelDetailControllerOnDidPressPlayButtonEvent(StandardLevelDetailViewController songDetailViewController)
		{
			var level = songDetailViewController.difficultyLevel.level;
			var song = CustomSongInfos.FirstOrDefault(x => x.levelId == level.levelID);
			if (song == null) return;
			Console.WriteLine("Song " + song.songName + " is selected. Setting volume to " + song.noteHitVolume);
			NoteHitVolumeChanger.SetVolume(song.noteHitVolume, song.noteMissVolume);
		}

		public void RefreshSongs()
		{
			if (SceneManager.GetActiveScene().buildIndex != MenuIndex) return;
			Log("Refreshing songs :)");
			var songs = RetrieveAllSongs();
			songs = songs.OrderBy(x => x.songName).ToList();

			var gameScenesManager = Resources.FindObjectsOfTypeAll<GameScenesManager>().FirstOrDefault();
			_levelCollections = Resources.FindObjectsOfTypeAll<LevelCollectionsForGameplayModes>().FirstOrDefault();
			var prevSoloLevels = _levelCollections.GetLevels(GameplayMode.SoloStandard).ToList();
			var prevOneSaberLevels = _levelCollections.GetLevels(GameplayMode.SoloOneSaber).ToList();

			foreach (var customSongInfo in CustomSongInfos)
			{
				prevSoloLevels.RemoveAll(x => x.levelID == customSongInfo.levelId);
				prevOneSaberLevels.RemoveAll(x => x.levelID == customSongInfo.levelId);
			}

			CustomLevels.Clear();
			CustomSongInfos.Clear();

			var i = 0;
			foreach (var song in songs)
			{
				i++;
				
				var id = song.GetIdentifier();
				if (songs.Any(x => x.levelId == id && x != song))
				{
					Log("Duplicate song found at " + song.path);
					continue;
				}

				CustomSongInfos.Add(song);

				var newLevel = ScriptableObject.CreateInstance<CustomLevel>();
				newLevel.Init(song);

				Stopwatch sendStopwatch = null;

				StartCoroutine(LoadAudio("file:///" + song.path + "/" + song.GetAudioPath(), newLevel));
				StartCoroutine(LoadSprite("file:///" + song.path + "/" + song.coverImagePath, newLevel));

				var newSceneInfo = ScriptableObject.CreateInstance<CustomSceneInfo>();
				newSceneInfo.Init(gameScenesManager, song.environmentName);

				var difficultyBeatmaps = new List<StandardLevelSO.DifficultyBeatmap>();
				foreach (var diffBeatmap in song.difficultyLevels)
				{
					try
					{
						var difficulty = diffBeatmap.difficulty.ToEnum(LevelDifficulty.Normal);

						if (!File.Exists(song.path + "/" + diffBeatmap.jsonPath))
						{
							Log("Couldn't find difficulty json " + song.path + "/" + diffBeatmap.jsonPath);
							continue;
						}

						var newBeatmapData = ScriptableObject.CreateInstance<BeatmapDataSO>();
						newBeatmapData.SetJsonData(diffBeatmap.json);
						
						var newDiffBeatmap = new StandardLevelSO.DifficultyBeatmap(newLevel, difficulty,
							diffBeatmap.difficultyRank, newBeatmapData);
						difficultyBeatmaps.Add(newDiffBeatmap);
					}
					catch (Exception e)
					{
						Log("Error parsing difficulty level in song: " + song.path);
						Log(e.Message);
					}
				}

				if (difficultyBeatmaps.Count == 0) continue;
				
				newLevel.SetDifficultyBeatmaps(difficultyBeatmaps.ToArray());
				newLevel.InitData();
				
				if (song.oneSaber)
				{
					prevOneSaberLevels.Add(newLevel);
				}
				else
				{
					prevSoloLevels.Add(newLevel);
				}

				CustomLevels.Add(newLevel);
			}

			var prevCollections =
				ReflectionUtil.GetPrivateField<LevelCollectionsForGameplayModes.LevelCollectionForGameplayMode[]>(
					_levelCollections, "_collections").ToList();
			var newSoloLevelsData = ScriptableObject.CreateInstance<CustomLevelCollectionStaticData>();
			newSoloLevelsData.Init(prevSoloLevels.ToArray());
			var newOneSaberLevelsData = ScriptableObject.CreateInstance<CustomLevelCollectionStaticData>();
			newOneSaberLevelsData.Init(prevOneSaberLevels.ToArray());

			var newSoloCollection = new CustomLevelCollection(GameplayMode.SoloStandard, newSoloLevelsData);
			var newOneSaberCollection = new CustomLevelCollection(GameplayMode.SoloOneSaber, newOneSaberLevelsData);
			var newNoArrowCollection = new CustomLevelCollection(GameplayMode.SoloNoArrows, newSoloLevelsData);
			var newPartyCollection = new CustomLevelCollection(GameplayMode.PartyStandard, newSoloLevelsData);
			prevCollections[0] = newSoloCollection;
			prevCollections[1] = newOneSaberCollection;
			prevCollections[2] = newNoArrowCollection;
			prevCollections[3] = newPartyCollection;

			ReflectionUtil.SetPrivateField(_levelCollections, "_collections", prevCollections.ToArray());
			SongsLoaded.Invoke();
		}

		private void RemoveCustomScores()
		{
			if (PlayerPrefs.HasKey("lbPatched")) return;
			_leaderboardScoreUploader = FindObjectOfType<LeaderboardScoreUploader>();
			if (_leaderboardScoreUploader == null) return;
			var scores =
				ReflectionUtil.GetPrivateField<List<LeaderboardScoreUploader.ScoreData>>(_leaderboardScoreUploader,
					"_scoresToUploadForCurrentPlayer");

			var scoresToRemove = new List<LeaderboardScoreUploader.ScoreData>();
			foreach (var scoreData in scores)
			{
				var split = scoreData._leaderboardId.Split('_');
				var levelID = split[0];
				if (CustomSongInfos.Any(x => x.levelId == levelID))
				{
					Log("Removing a custom score here");
					scoresToRemove.Add(scoreData);
				}
			}

			scores.RemoveAll(x => scoresToRemove.Contains(x));
		}

		private IEnumerator LoadAudio(string audioPath, CustomLevel customLevel)
		{
			AudioClip audioClip;
			if (!LoadedAudioClips.ContainsKey(audioPath))
			{
				using (var www = new WWW(audioPath))
				{
					yield return www;
					audioClip = www.GetAudioClip(true, true, AudioType.UNKNOWN);
					LoadedAudioClips.Add(audioPath, audioClip);
				}
			}
			else
			{
				audioClip = LoadedAudioClips[audioPath];
			}

			customLevel.SetAudioClip(audioClip);
		}

		private IEnumerator LoadSprite(string spritePath, CustomLevel customLevel)
		{
			Sprite sprite;
			if (!LoadedSprites.ContainsKey(spritePath))
			{
				using (var www = new WWW(spritePath))
				{
					yield return www;
					var tex = www.textureNonReadable;
					sprite = Sprite.Create(tex, new Rect(0, 0, 256, 256), Vector2.one * 0.5f, 100, 1);
					LoadedSprites.Add(spritePath, sprite);
				}
			}
			else
			{
				sprite = LoadedSprites[spritePath];
			}

			customLevel.SetCoverImage(sprite);
		}

		private List<CustomSongInfo> RetrieveAllSongs()
		{
			var customSongInfos = new List<CustomSongInfo>();
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

			foreach (var song in songFolders)
			{
				var results = Directory.GetFiles(song, "info.json", SearchOption.AllDirectories);
				if (results.Length == 0)
				{
					Log("Custom song folder '" + song + "' is missing info.json!");
					continue;
				}

				foreach (var result in results)
				{
					var songPath = Path.GetDirectoryName(result).Replace('\\', '/');
					var customSongInfo = GetCustomSongInfo(songPath);
					if (customSongInfo == null) continue;
					customSongInfos.Add(customSongInfo);
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

			return customSongInfos;
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
				diffLevels.Add(new CustomSongInfo.DifficultyLevel()
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
			Debug.Log("Song Loader: " + message);
			Console.WriteLine("Song Loader: " + message);
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.R))
			{
				RefreshSongs();
			}
		}
	}
}