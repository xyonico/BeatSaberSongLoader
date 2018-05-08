using UnityEngine;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SimpleJSON;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace SongLoaderPlugin
{
    public class SongLoader : MonoBehaviour
    {
        public static readonly UnityEvent SongsLoaded = new UnityEvent();
        public static readonly List<CustomSongInfo> CustomSongs = new List<CustomSongInfo>();

        public const int MenuIndex = 1;
        
        private MainGameSceneSetupData _sceneSetupData;
        private LeaderboardScoreUploader _leaderboardScoreUploader;
        private GameplayMode _prevMode;
        
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
            
            DontDestroyOnLoad(gameObject);
        }

        private void SceneManagerOnActiveSceneChanged(Scene arg0, Scene scene)
        {
            StartCoroutine(WaitRemoveScores());
            //We need to enable No Energy mode if playing custom song, so game doesn't ping leaderboards.
            var sceneSetup = FindObjectOfType<MainGameSceneSetup>();
            if (sceneSetup != null)
            {
                //We're in the main game
                _sceneSetupData = ReflectionUtil.GetPrivateField<MainGameSceneSetupData>(sceneSetup, "_mainGameSceneSetupData");
                var currentCustomSong = CustomSongs.FirstOrDefault(x => x.levelId == _sceneSetupData.levelId);
                if (currentCustomSong == null) return;
                _prevMode = _sceneSetupData.gameplayMode;
                //if (PlayerPrefs.GetInt("lbPatched", 0) == 1) return;
                //ReflectionUtil.SetPrivateField(_sceneSetupData, "gameplayMode", GameplayMode.PartyStandard);
            }
            else
            {
                //We're probably in menu
                if (_sceneSetupData == null) return;
                //Log("Resetting to previous gameplay mode " + _prevMode);
                //ReflectionUtil.SetPrivateProperty(_sceneSetupData, "gameplayMode", _prevMode);
            }
        }

        private IEnumerator WaitRemoveScores()
        {
            yield return new WaitForSecondsRealtime(1f);
            RemoveCustomScores();
        }

        public void RefreshSongs()
        {
            if (SceneManager.GetActiveScene().buildIndex != MenuIndex) return;
            var songs = RetrieveSongs();

            var gameScenesManager = Resources.FindObjectsOfTypeAll<GameScenesManager>().FirstOrDefault();

            var levelsData = new List<LevelStaticData>();
            var gameDataModel = PersistentSingleton<GameDataModel>.instance;
            var oldData = gameDataModel.gameStaticData.worldsData[0].levelsData.ToList();

            foreach (var customSongInfo in CustomSongs)
            {
                oldData.RemoveAll(x => x.levelId == customSongInfo.levelId);
            }
            
            CustomSongs.Clear();
            
            foreach (var song in songs)
            {
                var id = song.GetIdentifier();
                CustomSongs.Add(song);
                
                LevelStaticData newLevel = null;
                try
                {
                    newLevel = ScriptableObject.CreateInstance<LevelStaticData>();
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
                    }
                    
                    ReflectionUtil.SetPrivateField(newDiffLevel, "_songLevelData", newSongLevelData);
                    StartCoroutine(LoadAudio("file://" + song.path + "/" + diffLevel.audioPath, newDiffLevel, "_audioClip"));
                    difficultyLevels.Add(newDiffLevel);
                }
                
                ReflectionUtil.SetPrivateField(newLevel, "_difficultyLevels", difficultyLevels.ToArray());
                newLevel.OnEnable();
                levelsData.Add(newLevel);
            }
            
            oldData.AddRange(levelsData);
            ReflectionUtil.SetPrivateField(gameDataModel.gameStaticData.worldsData[0], "_levelsData", oldData.ToArray());
            SongsLoaded.Invoke();
        }

        private void RemoveCustomScores()
        {
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
                if (CustomSongs.Any(x => x.levelId == levelID))
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
                ReflectionUtil.SetPrivateField(obj, fieldName, www.GetAudioClip());
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

        private List<CustomSongInfo> RetrieveSongs()
        {
            var customSongInfos = new List<CustomSongInfo>();
            var path = Environment.CurrentDirectory;
            path = path.Replace('\\', '/');
            
            var songFolders = Directory.GetDirectories(path + "/CustomSongs");
            foreach (var song in songFolders)
            {
                var songPath = song.Replace('\\', '/');
                if (!File.Exists(songPath + "/info.json"))
                {
                    Log("Custom song folder '" + songPath + "' is missing info.json!");
                    continue;
                }

                var infoText = File.ReadAllText(songPath + "/info.json");
                var songInfo = JsonUtility.FromJson<CustomSongInfo>(infoText);
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
                customSongInfos.Add(songInfo);
            }

            return customSongInfos;
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
