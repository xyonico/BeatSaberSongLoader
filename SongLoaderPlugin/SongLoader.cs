using UnityEngine;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SimpleJSON;
using SongLoaderPlugin.Internals;
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

        private LeaderboardScoreUploader _leaderboardScoreUploader;
        private SongSelectionMasterViewController _songSelectionView;
        private DifficultyViewController _difficultyView;

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

            var songListController = Resources.FindObjectsOfTypeAll<SongListViewController>().FirstOrDefault();
            if (songListController == null) return;
            songListController.didSelectSongEvent += OnDidSelectSongEvent;

            _songSelectionView = Resources.FindObjectsOfTypeAll<SongSelectionMasterViewController>().FirstOrDefault();
            _difficultyView = Resources.FindObjectsOfTypeAll<DifficultyViewController>().FirstOrDefault();
        }

        private IEnumerator WaitRemoveScores()
        {
            yield return new WaitForSecondsRealtime(1f);
            RemoveCustomScores();
        }

        //To fix the bug explained in CustomLevelStaticData.cs
        private void OnDidSelectSongEvent(SongListViewController songListViewController)
        {
            var song = CustomLevelStaticDatas.FirstOrDefault(x => x.levelId == songListViewController.levelId);
            if (song == null) return;
            if (song.difficultyLevels.All(x => x.difficulty != _songSelectionView.difficulty))
            {
                var isDiffSelected =
                    ReflectionUtil.GetPrivateField<bool>(_difficultyView, "_difficultySelected");
                if (!isDiffSelected) return;
                //The new selected song does not have the current difficulty selected
                var firstDiff = song.difficultyLevels.FirstOrDefault();
                if (firstDiff == null) return;
                ReflectionUtil.SetPrivateField(_songSelectionView, "_difficulty", firstDiff.difficulty);
            }
        }

        public void RefreshSongs()
        {
            if (SceneManager.GetActiveScene().buildIndex != MenuIndex) return;
            Log("Refreshing songs");
            var songs = RetrieveAllSongs();
            songs = songs.OrderBy(x => x.songName).ToList();

            var gameScenesManager = Resources.FindObjectsOfTypeAll<GameScenesManager>().FirstOrDefault();

            var gameDataModel = PersistentSingleton<GameDataModel>.instance;
            var oldData = gameDataModel.gameStaticData.worldsData[0].levelsData.ToList();

            foreach (var customSongInfo in CustomSongInfos)
            {
                oldData.RemoveAll(x => x.levelId == customSongInfo.levelId);
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

                CustomLevelStaticData newLevel = null;
                try
                {
                    newLevel = ScriptableObject.CreateInstance<CustomLevelStaticData>();
                }
                catch (Exception e)
                {
                    //LevelStaticData.OnEnable throws null reference exception because we don't have time to set _difficultyLevels
                }

                ReflectionUtil.SetPrivateField(newLevel, "_levelId", id);
                ReflectionUtil.SetPrivateField(newLevel, "_authorName", song.authorName);
                ReflectionUtil.SetPrivateField(newLevel, "_songName", song.songName);
                ReflectionUtil.SetPrivateField(newLevel, "_songSubName", song.songSubName);
                ReflectionUtil.SetPrivateField(newLevel, "_previewStartTime", song.previewStartTime);
                ReflectionUtil.SetPrivateField(newLevel, "_previewDuration", song.previewDuration);
                ReflectionUtil.SetPrivateField(newLevel, "_beatsPerMinute", song.beatsPerMinute);
                StartCoroutine(LoadSprite("file://" + song.path + "/" + song.coverImagePath, newLevel, "_coverImage"));

                var newSceneInfo = ScriptableObject.CreateInstance<SceneInfo>();
                ReflectionUtil.SetPrivateField(newSceneInfo, "_gameScenesManager", gameScenesManager);
                ReflectionUtil.SetPrivateField(newSceneInfo, "_sceneName", song.environmentName);

                ReflectionUtil.SetPrivateField(newLevel, "_environmetSceneInfo", newSceneInfo);

                var difficultyLevels = new List<LevelStaticData.DifficultyLevel>();
                foreach (var diffLevel in song.difficultyLevels)
                {
                    var newDiffLevel = new LevelStaticData.DifficultyLevel();

                    try
                    {
                        var difficulty = diffLevel.difficulty.ToEnum(LevelStaticData.Difficulty.Normal);
                        ReflectionUtil.SetPrivateField(newDiffLevel, "_difficulty", difficulty);
                        ReflectionUtil.SetPrivateField(newDiffLevel, "_difficultyRank", diffLevel.difficultyRank);

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

                        ReflectionUtil.SetPrivateField(newDiffLevel, "_songLevelData", newSongLevelData);
                        StartCoroutine(LoadAudio("file://" + song.path + "/" + diffLevel.audioPath, newDiffLevel,
                            "_audioClip"));
                        difficultyLevels.Add(newDiffLevel);
                    }
                    catch (Exception e)
                    {
                        Log("Error parsing difficulty level in song: " + song.path);
                        Log(e.Message);
                        continue;
                    }
                }

                if (difficultyLevels.Count == 0) continue;

                ReflectionUtil.SetPrivateField(newLevel, "_difficultyLevels", difficultyLevels.ToArray());
                newLevel.OnEnable();
                oldData.Add(newLevel);
                CustomLevelStaticDatas.Add(newLevel);
            }

            ReflectionUtil.SetPrivateField(gameDataModel.gameStaticData.worldsData[0], "_levelsData",
                oldData.ToArray());
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
            using (var www = new WWW(audioPath))
            {
                yield return www;
                ReflectionUtil.SetPrivateField(obj, fieldName, www.GetAudioClip(true, true, AudioType.UNKNOWN));
            }
        }

        private IEnumerator LoadSprite(string spritePath, object obj, string fieldName)
        {
            Texture2D tex;
            tex = new Texture2D(256, 256, TextureFormat.DXT1, false);
            using (WWW www = new WWW(spritePath))
            {
                yield return www;
                www.LoadImageIntoTexture(tex);
                var newSprite = Sprite.Create(tex, new Rect(0, 0, 256, 256), Vector2.one * 0.5f, 100, 1);
                ReflectionUtil.SetPrivateField(obj, fieldName, newSprite);
            }
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
