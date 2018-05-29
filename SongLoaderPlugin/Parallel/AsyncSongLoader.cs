using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SongLoaderPlugin.Internals;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SongLoaderPlugin.Parallel {
    public class AsyncSongLoader : SongLoader {
        private ThreadHandler handler;

        private List<Button> _buttonInstances;

        public new static void OnLoad() {
            OnLoadImpl<AsyncSongLoader>();
        }

        private void Awake() {
            Instance = this;

            handler = gameObject.AddComponent<ThreadHandler>();

            Button[] btns = Resources.FindObjectsOfTypeAll<Button>();

            _buttonInstances = btns.Where(x => x.name == "TutorialButton").ToList();

            RefreshSongs();
            SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;
            SceneManagerOnActiveSceneChanged(new Scene(), new Scene());

            DontDestroyOnLoad(gameObject);
        }

        public void SetButtonText(string text) {
            foreach (Button t in _buttonInstances)
                SetButtonText(t, text);
        }

        public void SetButtonText(Button _button, string _text) {
            if (_button.GetComponentInChildren<TextMeshProUGUI>() == null) return;

            foreach (TextMeshProUGUI c in _button.GetComponentsInChildren<TextMeshProUGUI>())
                c.text = _text;
        }

        public string GetButtonText(Button _button) {
            return _button.GetComponentInChildren<TextMeshProUGUI>()?.text;
        }

        public override void RefreshSongs() {
            if (SceneManager.GetActiveScene().buildIndex != MenuIndex) return;

            var gameScenesManager = Resources.FindObjectsOfTypeAll<GameScenesManager>().FirstOrDefault();

            var gameDataModel = PersistentSingleton<GameDataModel>.instance;
            var oldData = gameDataModel.gameStaticData.worldsData[0].levelsData.ToList();

            CustomLevelStaticDatas.Clear();
            CustomSongInfos.Clear();

            var loadedSongs = new List<CustomSongInfo>();

            SetButtonText("Loading songs");

            // TODO Find way to ensure song order
            RetrieveAllSongsAsync(song => {
                oldData.RemoveAll(x => x.levelId == song.levelId);

                var id = song.GetIdentifier();

                if (loadedSongs.Any(x => x.levelId == id && x != song)) {
                    Log("Duplicate song found at " + song.path);
                    return;
                }

                loadedSongs.Add(song);

                CustomLevelStaticData newLevel = null;
                try {
                    newLevel = ScriptableObject.CreateInstance<CustomLevelStaticData>();
                }
                catch (Exception) {
                    //LevelStaticData.OnEnable throws null reference exception because we don't have time to set _difficultyLevels
                    Log("Unable to create new level");
                    return;
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
                foreach (var diffLevel in song.difficultyLevels) {
                    var newDiffLevel = new LevelStaticData.DifficultyLevel();

                    try {
                        var difficulty = diffLevel.difficulty.ToEnum(LevelStaticData.Difficulty.Normal);
                        ReflectionUtil.SetPrivateField(newDiffLevel, "_difficulty", difficulty);
                        ReflectionUtil.SetPrivateField(newDiffLevel, "_difficultyRank", diffLevel.difficultyRank);

                        if (!File.Exists(song.path + "/" + diffLevel.jsonPath)) {
                            Log("Couldn't find difficulty json " + song.path + "/" + diffLevel.jsonPath);
                            continue;
                        }

                        var newSongLevelData = ScriptableObject.CreateInstance<SongLevelData>();
                        var json = File.ReadAllText(song.path + "/" + diffLevel.jsonPath);
                        try {
                            newSongLevelData.LoadFromJson(json);
                        }
                        catch (Exception e) {
                            Log("Error while parsing " + song.path + "/" + diffLevel.jsonPath);
                            Log(e.ToString());
                            continue;
                        }

                        ReflectionUtil.SetPrivateField(newDiffLevel, "_songLevelData", newSongLevelData);
                        StartCoroutine(LoadAudio("file://" + song.path + "/" + diffLevel.audioPath, newDiffLevel,
                                                 "_audioClip"));
                        difficultyLevels.Add(newDiffLevel);
                    }
                    catch (Exception e) {
                        Log("Error parsing difficulty level in song: " + song.path);
                        Log(e.Message);
                        continue;
                    }
                }

                if (difficultyLevels.IsEmpty()) return;

                ReflectionUtil.SetPrivateField(newLevel, "_difficultyLevels", difficultyLevels.ToArray());
                newLevel.OnEnable();
                oldData.Add(newLevel);
                CustomLevelStaticDatas.Add(newLevel);

            }, () => {
                SetButtonText("Tutorial");
                ReflectionUtil.SetPrivateField(gameDataModel.gameStaticData.worldsData[0], "_levelsData", oldData.ToArray());
                SongsLoaded.Invoke();
            });
        }

        private void RetrieveAllSongsAsync(Action<CustomSongInfo> task, ThreadStart onFinished = null) {
            handler.Dispatch(() => {
                var path = Environment.CurrentDirectory;
                path = path.Replace('\\', '/');

                var currentHashes = new List<string>();
                var cachedSongs = new string[0];
                if (Directory.Exists(path + "/CustomSongs/.cache")) {
                    cachedSongs = Directory.GetDirectories(path + "/CustomSongs/.cache");
                }
                else {
                    Directory.CreateDirectory(path + "/CustomSongs/.cache");
                }

                var songZips = Directory.GetFiles(path + "/CustomSongs")
                                        .Where(x => x.ToLower().EndsWith(".zip") || x.ToLower().EndsWith(".beat"))
                                        .ToArray();
                foreach (var songZip in songZips) {
                    Log("Found zip: " + songZip);
                    //Check cache if zip already is extracted
                    string hash;
                    if (Utils.CreateMD5FromFile(songZip, out hash)) {
                        currentHashes.Add(hash);
                        if (cachedSongs.Any(x => x.Contains(hash))) continue;

                        using (var unzip = new Unzip(songZip)) {
                            unzip.ExtractToDirectory(path + "/CustomSongs/.cache/" + hash);
                            Log("Extracted to " + path + "/CustomSongs/.cache/" + hash);
                        }
                    }
                    else {
                        Log("Error reading zip " + songZip);
                    }
                }

                var songFolders = Directory.GetDirectories(path + "/CustomSongs").ToList();
                var songCaches = Directory.GetDirectories(path + "/CustomSongs/.cache");

                foreach (var song in songFolders) {
                    LoadSong(song, task);
                }

                foreach (var song in songCaches) {
                    var hash = Path.GetFileName(song);
                    if (!currentHashes.Contains(hash)) {
                        //Old cache
                        Log("Deleting old cache: " + song);
                        Directory.Delete(song, true);
                    }
                }

                if(onFinished != null)
                    handler.Post(onFinished);
            });
        }

        private void LoadSong(string song, Action<CustomSongInfo> task) {
            var results = Directory.GetFiles(song, "info.json", SearchOption.AllDirectories);
            if (results.Length == 0) {
                Log("Custom song folder '" + song + "' is missing info.json!");
                return;
            }

            foreach (var result in results) {
                var songPath = Path.GetDirectoryName(result)?.Replace('\\', '/');
                var customSongInfo = GetCustomSongInfo(songPath);
                if (customSongInfo == null) continue;
                handler.Post(() => {
                    SetButtonText(customSongInfo.songName);
                    task(customSongInfo);
                });
            }
        }
    }
}