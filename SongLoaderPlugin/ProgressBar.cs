using System.Collections.Generic;
using System.Collections;
using SongLoaderPlugin.OverrideClasses;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SongLoaderPlugin
{
	public class ProgressBar : MonoBehaviour
	{
		private Canvas _canvas;
		private TMP_Text _headerText;
		private Image _loadingBackg;
		private Image _loadingBar;

		private static readonly Vector3 Position = new Vector3(0, 2.5f, 2.5f);
		private static readonly Vector3 Rotation = new Vector3(0, 0, 0);
		private static readonly Vector3 Scale = new Vector3(0.01f, 0.01f, 0.01f);
		
		private static readonly Vector2 CanvasSize = new Vector2(100, 50);
		
		private static readonly Vector2 HeaderPosition = new Vector2(0, 15);
		private static readonly Vector2 HeaderSize = new Vector2(100, 20);
		private const string HeaderText = "Loading songs...";
		private const float HeaderFontSize = 15f;
		
		private static readonly Vector2 LoadingBarSize = new Vector2(100, 10);
		private static readonly Color BackgroundColor = new Color(0, 0, 0, 0.2f);

		public static void Create()
		{
			new GameObject("Progress Bar").AddComponent<ProgressBar>();
		}

		private void OnEnable()
		{
			SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;
			SongLoader.LoadingStartedEvent += SongLoaderOnLoadingStartedEvent;
			SongLoader.SongsLoadedEvent += SongLoaderOnSongsLoadedEvent;
		}

		private void OnDisable()
		{
			SceneManager.activeSceneChanged -= SceneManagerOnActiveSceneChanged;
			SongLoader.LoadingStartedEvent -= SongLoaderOnLoadingStartedEvent;
			SongLoader.SongsLoadedEvent -= SongLoaderOnSongsLoadedEvent;
		}

		private void SceneManagerOnActiveSceneChanged(Scene arg0, Scene arg1)
		{
			_canvas.enabled = false;
		}

		private void SongLoaderOnLoadingStartedEvent(SongLoader obj)
		{
			_headerText.text = HeaderText;
			_canvas.enabled = true;
		}

		private void SongLoaderOnSongsLoadedEvent(SongLoader arg1, List<CustomLevel> arg2)
		{
			_headerText.text = arg2.Count + " songs loaded";
			StartCoroutine(SongsLoadedRoutine());
		}

		private IEnumerator SongsLoadedRoutine()
		{
			yield return new WaitForSecondsRealtime(5f);
			_canvas.enabled = false;
		}

		private void Awake()
		{
			gameObject.transform.position = Position;
			gameObject.transform.eulerAngles = Rotation;
			gameObject.transform.localScale = Scale;
			
			_canvas = gameObject.AddComponent<Canvas>();
			_canvas.renderMode = RenderMode.WorldSpace;
			_canvas.enabled = false;
			var rectTransform = _canvas.transform as RectTransform;
			rectTransform.sizeDelta = CanvasSize;

			_headerText = new GameObject("Header").AddComponent<TextMeshProUGUI>();
			rectTransform = _headerText.transform as RectTransform;
			rectTransform.SetParent(_canvas.transform, false);
			rectTransform.anchoredPosition = HeaderPosition;
			rectTransform.sizeDelta = HeaderSize;
			_headerText.text = HeaderText;
			_headerText.fontSize = HeaderFontSize;
			
			_loadingBackg = new GameObject("Background").AddComponent<Image>();
			rectTransform = _loadingBackg.transform as RectTransform;
			rectTransform.SetParent(_canvas.transform, false);
			rectTransform.sizeDelta = LoadingBarSize;
			_loadingBackg.color = BackgroundColor;

			_loadingBar = new GameObject("Loading Bar").AddComponent<Image>();
			rectTransform = _loadingBar.transform as RectTransform;
			rectTransform.SetParent(_canvas.transform, false);
			rectTransform.sizeDelta = LoadingBarSize;
			var tex = Texture2D.whiteTexture;
			var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, 100, 1);
			_loadingBar.sprite = sprite;
			_loadingBar.type = Image.Type.Filled;
			_loadingBar.fillMethod = Image.FillMethod.Horizontal;
			_loadingBar.color = new Color(1, 1, 1, 0.5f);
			
			DontDestroyOnLoad(gameObject);
		}

		private void Update()
		{
			if (!_canvas.enabled) return;
			_loadingBar.fillAmount = SongLoader.LoadingProgress;
		}
	}
}