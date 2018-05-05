# BeatSaberSongInjector
An injector for adding custom songs into Beat Saber.

*This mod works on both the Steam and Oculus Store versions.*

## Installation Instructions
 1. Extract the .zip file into the `Oculus Apps\Software\hyperbolic-magnetism-beat-saber` for Oculus Home OR `steamapps\common\Beat Saber` for Steam. (The one with Beat Saber.exe)
  
    The Beat Saber folder should look something like this:
    * `Beat Saber_Data`
    * `BSSI`
    * `CustomSongs`
    * `Beat Saber.exe`
    * `UnityPlayer.dll`
 2. Done!

## Usage
 1. Launch Beat Saber through the platform you purchased it on.
 2. While at the main menu, launch `BSSI/BSSI.exe` and press <kbd>Inject</kbd> (If your game crashes go to troubleshooting)	
 3. Go to 'Solo' -> 'Standard' and your custom song will be available to play at the bottom of the list.	


## Installing Custom Songs
The following files must be placed within their own folder inside the "CustomSongs" folder.

    Required files:
		1. cover.jpg (Size 256x256)
			-This is the picture shown next to song in the selection screen.
			-The name can be whatever you want, make sure its the same as the one found in info.json
			-Only supported image types are jpg and png
		2. song.wav / song.ogg
			-This is your song you would like to inject
			-Name must be the same as in info.json
			-Only supported audio types are wav and ogg
		3. easy.json / normal.json / hard.json / expert.json
			-This is the note chart for each difficulty
			-Names must match the "jsonPath" in info.json
			-Use a Beat Saber editor to make your own note chart for the song
		4. info.json
			-Contains the info for the song

The following is a template for you to use:
```json
{
  "songName":"YourSongName",
  "songSubName":"ft. Name",
  "authorName":"AuthorName",
  "beatsPerMinute":179.0, 
  "previewStartTime":12.0,
  "previewDuration":10.0,
  "coverImagePath":"cover.jpg",
  "environmentName":"DefaultEnvironment",
  "difficultyLevels": [
	{ "difficulty":"Expert", "difficultyRank":4, "audioPath":"YourSong.wav", "jsonPath":"expert.json" },
	{ "difficulty":"Easy", "difficultyRank":0, "audioPath":"YourSong.ogg", "jsonPath":"easy.json" }
  ]
}
```
___

### info.json Explanation
```
"songName" - Name of your song
"songSubName" - Text rendered in smaller letters next to song name. (ft. Artist)
"beatsPerMinute" - BPM of the song you are using
"previewStartTime" - How many seconds into the song the preview should start
"previewDuration" - Time in seconds the song will be previewed in selection screen
"coverImagePath" - Cover image name
"environmentName" - Game environment to be used

All possible environmentNames:
-DefaultEnvironment
-BigMirrorEnvironment
-TriangleEnvironment
-NiceEnvironment

"difficultyLevels": [
	{ "difficulty": This is the difficulty to show in game, "difficultyRank": Unimportant difficulty ranking, "audioPath":This is your song .wav/.ogg file, "jsonPath":This is your difficulty .json file }
  ]
```
___

## Troubleshooting
BSSI can be finicky at times and stubborn with injecting.

Steps to help you avoid crashing:
 * Wait at least 5 seconds after starting Beat Saber before injecting.
 * Put your hand/face over the Oculus Face Sensor so the black mirrored screen on your desktop shows the game before launching BSSI.
 * Make sure there are no 'ghost processes' running in task manager labelled 'BSSI' & 'MonoJunkie'; if there is, end the task.
 * Open Beat Saber before opening BSSI, running as administrator for both is preferred.
 * Do a clean install of BSSI if crashing persists.
