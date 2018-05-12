using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SongLoaderPlugin {
    public class SongLoaderUI : MonoBehaviour {
        private static SongLoaderUI _instance;

        private Button _settingsButton; //used as a reference to the mainmenu settings button
        public Button RefreshSongsButton; //our button on the menu

        private RectTransform _mainMenuRectTransform; //the main menu object
        private SongLoader _songLoaderObject; //the song loader object


        internal static void OnLoad() {
            if (_instance != null) return;
            new GameObject("SongLoader UI").AddComponent<SongLoaderUI>();
        }

        void Awake() {
            _instance = this;

            #region Retrieve menu objects

            try {
                _songLoaderObject = Resources.FindObjectsOfTypeAll<SongLoader>()[0]; //attempt to find the SongLoader script
                _settingsButton = Resources.FindObjectsOfTypeAll<Button>().First(o => o.name == "SettingsButton"); //Get the settings button
                _mainMenuRectTransform = (RectTransform) _settingsButton.transform.parent; //get the MainMenu object
            }
            catch (Exception ex) {
                Debug.Log($"Error retrieving menu buttons: {ex.Message}");
            }

            #endregion

            #region Create Button

            try {
                CreateButton(); //attempts to create our own menu button
            }
            catch (Exception ex) {
                Debug.Log($"Error when creating button: {ex.Message}");
            }

            #endregion
        }
        
        void CreateButton() {
            if (_settingsButton == null) return; //if the template button doesn't exist, don't try

            RefreshSongsButton = Instantiate(_settingsButton, _mainMenuRectTransform, false); //create a copy of the settings button
            DestroyImmediate(RefreshSongsButton.GetComponent<GameEventOnUIButtonClick>()); //destroy its click handler script
            RefreshSongsButton.onClick = new Button.ButtonClickedEvent(); //create our own click handler
            try {
                ((RectTransform) RefreshSongsButton.transform).anchoredPosition += new Vector2(0f, 11.5f); //increase the y pos of the button by 15f
                //((RectTransform) RefreshSongsButton.transform).sizeDelta = new Vector2(28f, 10f);

                if (RefreshSongsButton.GetComponentInChildren<TextMeshProUGUI>() != null) { //if the button can have text
                    RefreshSongsButton.GetComponentInChildren<TextMeshProUGUI>().text = "Refresh Songs"; //set the text to 'Refresh Songs'
                }

                RefreshSongsButton.onClick.AddListener(() => {
                    if (_songLoaderObject == null) return; //if the songLoader gameobject is in the scene
                    _songLoaderObject.RefreshSongs(); //refresh the currently loaded songs
                });
            }
            catch (Exception e) {
                Debug.Log("setting up button threw an error: " + e.Message);
            }

            Debug.Log("Finished button creation");
        }
    }
}