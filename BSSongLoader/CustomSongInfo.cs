using System;
using System.Collections.Generic;
using System.Linq;

namespace BSSongLoader
{
   [Serializable]
   public class CustomSongInfo
    {
        public string songName;
        public string songSubName;
        public string authorName;
        public float beatsPerMinute;
        public float previewStartTime;
        public float previewDuration;
        public string environmentName;
        public string coverImagePath;
        public DifficultyLevel[] difficultyLevels;
        public string path;

        [Serializable]
        public class DifficultyLevel
        {
            public string difficulty;
            public int difficultyRank;
            public string audioPath;
            public string jsonPath;
        }
    }
}
