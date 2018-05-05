using UnityEngine;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SimpleJSON;

namespace BSSongLoader
{
    public class SongInjector : MonoBehaviour
    {
        public static void OnLoad()
        {
            new GameObject("Song Injector").AddComponent<SongInjector>();
        }

        private void Awake()
        {
            var songs = RetrieveSongs();

            var gameScenesManager = Resources.FindObjectsOfTypeAll<GameScenesManager>().FirstOrDefault();

            var levelsData = new List<LevelStaticData>();
            foreach (var song in songs)
            {
                LevelStaticData newLevel = null;
                try
                {
                    newLevel = ScriptableObject.CreateInstance<LevelStaticData>();
                }
                catch (Exception e)
                {
                    //LevelStaticData.OnEnable throws null reference exception because we don't have time to set _difficultyLevels
                }
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
                    ReflectionUtil.SetPrivateField(newDiffLevel, "_difficulty", Enum.Parse(typeof(LevelStaticData.Difficulty), diffLevel.difficulty));
                    ReflectionUtil.SetPrivateField(newDiffLevel, "_difficultyRank", diffLevel.difficultyRank);

                    if (!File.Exists(song.path + "/" + diffLevel.jsonPath))
                    {
                        Debug.LogError("Couldn't find " + diffLevel.jsonPath + " in path " + song.path);
                        continue;
                    }
                    
                    var newSongLevelData = ScriptableObject.CreateInstance<SongLevelData>();
                    var json = File.ReadAllText(song.path + "/" + diffLevel.jsonPath);

                    newSongLevelData.LoadFromJson(json);
                    ReflectionUtil.SetPrivateField(newDiffLevel, "_songLevelData", newSongLevelData);
                    StartCoroutine(LoadAudio("file://" + song.path + "/" + diffLevel.audioPath, newDiffLevel, "_audioClip"));
                    difficultyLevels.Add(newDiffLevel);
                }
                
                ReflectionUtil.SetPrivateField(newLevel, "_difficultyLevels", difficultyLevels.ToArray());
                ReflectionUtil.InvokePrivateMethod(newLevel, "OnEnable", null);

                ReflectionUtil.SetPrivateField(newLevel, "_levelId", newLevel.GetHashCode().ToString());

                levelsData.Add(newLevel);
            }

            var gameDataModel = PersistentSingleton<GameDataModel>.instance;
            var oldData = gameDataModel.gameStaticData.worldsData[0].levelsData.ToList();
            oldData.AddRange(levelsData);
            ReflectionUtil.SetPrivateField(gameDataModel.gameStaticData.worldsData[0], "_levelsData", oldData.ToArray());

            DontDestroyOnLoad(gameObject);
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
            var path = Application.dataPath;
            path += "/../";
            
            var songFolders = Directory.GetDirectories(path + "/CustomSongs");
            foreach (var song in songFolders)
            {
                if (!File.Exists(song + "/info.json"))
                {
                    Debug.LogError("Custom song folder '" + song + "' is missing info.json!");
                    continue;
                }

                var infoText = File.ReadAllText(song + "/info.json");
                var songInfo = JsonUtility.FromJson<CustomSongInfo>(infoText);
                songInfo.path = song;

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
    }
}
