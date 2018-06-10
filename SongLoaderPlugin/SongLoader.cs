using UnityEngine;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SimpleJSON;
using SongLoaderPlugin.Internals;
using SongLoaderPlugin.OverrideClasses;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace SongLoaderPlugin
{
	public class SongLoader : MonoBehaviour
	{
		public static readonly UnityEvent SongsLoaded = new UnityEvent();
		public static readonly List<CustomSongInfo> CustomSongInfos = new List<CustomSongInfo>();
		public static readonly List<CustomLevelStaticData> CustomLevelStaticDatas = new List<CustomLevelStaticData>();

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
				var songDetailController = Resources.FindObjectsOfTypeAll<SongDetailViewController>().FirstOrDefault();
				if (songDetailController == null) return;
				songDetailController.didPressPlayButtonEvent += SongDetailControllerOnDidPressPlayButtonEvent;
			}
			else if (scene.buildIndex == 4)
			{
				if (NoteHitVolumeChanger.PrefabFound) return;
				var mainGameData = Resources.FindObjectsOfTypeAll<MainGameSceneSetupData>().FirstOrDefault();
				if (mainGameData == null) return;
				var level = mainGameData.difficultyLevel.level;
				var song = CustomSongInfos.FirstOrDefault(x => x.levelId == level.levelId);
				if (song == null) return;
				Console.WriteLine("Song " + song.songName + " is selected. Setting volume to " + song.noteHitVolume);
				NoteHitVolumeChanger.SetVolume(song.noteHitVolume, song.noteMissVolume);
			}
		}

		private IEnumerator WaitRemoveScores()
		{
			yield return new WaitForSecondsRealtime(1f);
			RemoveCustomScores();
		}

		private void SongDetailControllerOnDidPressPlayButtonEvent(SongDetailViewController songDetailViewController)
		{
			var level = songDetailViewController.difficultyLevel.level;
			var song = CustomSongInfos.FirstOrDefault(x => x.levelId == level.levelId);
			if (song == null) return;
			Console.WriteLine("Song " + song.songName + " is selected. Setting volume to " + song.noteHitVolume);
			NoteHitVolumeChanger.SetVolume(song.noteHitVolume, song.noteMissVolume);
		}

		public void RefreshSongs()
		{
			if (SceneManager.GetActiveScene().buildIndex != MenuIndex) return;
			Log("Refreshing songs");
			var songs = RetrieveAllSongs();
			songs = songs.OrderBy(x => x.songName).ToList();

			var gameScenesManager = Resources.FindObjectsOfTypeAll<GameScenesManager>().FirstOrDefault();
			_levelCollections = Resources.FindObjectsOfTypeAll<LevelCollectionsForGameplayModes>().FirstOrDefault();
			var prevSoloLevels = _levelCollections.GetLevels(GameplayMode.SoloStandard).ToList();
			var prevOneSaberLevels = _levelCollections.GetLevels(GameplayMode.SoloOneSaber).ToList();

			foreach (var customSongInfo in CustomSongInfos)
			{
				prevSoloLevels.RemoveAll(x => x.levelId == customSongInfo.levelId);
				prevOneSaberLevels.RemoveAll(x => x.levelId == customSongInfo.levelId);
			}

			CustomLevelStaticDatas.Clear();
			CustomSongInfos.Clear();

			foreach (var song in songs)
			{
				var id = song.GetIdentifier();
				if (songs.Any(x => x.levelId == id && x != song))
				{
					Log("Duplicate song found at " + song.path);
					continue;
				}

				CustomSongInfos.Add(song);

				var newLevel = ScriptableObject.CreateInstance<CustomLevelStaticData>();

				StartCoroutine(LoadAudio("file://" + song.path + "/" + song.GetAudioPath(), newLevel, "_audioClip"));
				StartCoroutine(LoadSprite("file://" + song.path + "/" + song.coverImagePath, newLevel, "_coverImage"));

				var newSceneInfo = ScriptableObject.CreateInstance<CustomSceneInfo>();
				newSceneInfo.Init(gameScenesManager, song.environmentName);

				var difficultyLevels = new List<LevelStaticData.DifficultyLevel>();
				foreach (var diffLevel in song.difficultyLevels)
				{
					try
					{
						var difficulty = diffLevel.difficulty.ToEnum(LevelDifficulty.Normal);

						if (!File.Exists(song.path + "/" + diffLevel.jsonPath))
						{
							Log("Couldn't find difficulty json " + song.path + "/" + diffLevel.jsonPath);
							continue;
						}

						var newSongLevelData = ScriptableObject.CreateInstance<SongLevelData>();
						var json = File.ReadAllText(song.path + "/" + diffLevel.jsonPath);
						try
						{
							newSongLevelData.LoadFromJson(json);
						}
						catch (Exception e)
						{
							Log("Error while parsing " + song.path + "/" + diffLevel.jsonPath);
							Log(e.ToString());
							continue;
						}

						var newDiffLevel = new LevelStaticData.DifficultyLevel(newLevel, difficulty,
							diffLevel.difficultyRank, newSongLevelData);
						difficultyLevels.Add(newDiffLevel);
					}
					catch (Exception e)
					{
						Log("Error parsing difficulty level in song: " + song.path);
						Log(e.Message);
					}
				}

				if (difficultyLevels.Count == 0) continue;

				newLevel.Init(id, song.songName, song.songSubName, song.authorName, song.beatsPerMinute,
					song.previewStartTime, song.previewDuration, newSceneInfo, difficultyLevels.ToArray());
				newLevel.OnEnable();
				if (song.gamemodeType == 0)
				{
					prevSoloLevels.Add(newLevel);
				}
				else if (song.gamemodeType == 1)
				{
					prevOneSaberLevels.Add(newLevel);
				}

				CustomLevelStaticDatas.Add(newLevel);
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

		private IEnumerator LoadAudio(string audioPath, object obj, string fieldName)
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

			ReflectionUtil.SetPrivateField(obj, fieldName, audioClip);
		}

		private IEnumerator LoadSprite(string spritePath, object obj, string fieldName)
		{
			Sprite sprite;
			if (!LoadedSprites.ContainsKey(spritePath))
			{
				var tex = new Texture2D(256, 256, TextureFormat.DXT1, false);
				using (var www = new WWW(spritePath))
				{
					yield return www;
					www.LoadImageIntoTexture(tex);
					sprite = Sprite.Create(tex, new Rect(0, 0, 256, 256), Vector2.one * 0.5f, 100, 1);
					LoadedSprites.Add(spritePath, sprite);
				}
			}
			else
			{
				sprite = LoadedSprites[spritePath];
			}

			ReflectionUtil.SetPrivateField(obj, fieldName, sprite);
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
			catch (Exception e)
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