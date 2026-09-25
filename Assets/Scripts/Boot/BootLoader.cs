using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Zenject;

namespace Clock.Boot
{
    public sealed class BootLoader : MonoBehaviour
    {
        [SerializeField] private GameObject _loadingScreen;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Camera _bootCamera;

        private string _gameSceneAddress;
        private AsyncOperationHandle<IResourceLocator> _initialization;
        private AsyncOperationHandle<SceneInstance> _sceneHandle;
        private bool _isQuitting;

        [Inject]
        private void Construct([Inject(Id = "GameplayScene")] string gameSceneAddress)
        {
            _gameSceneAddress = gameSceneAddress;
        }

        private IEnumerator Start()
        {
            _loadingScreen.SetActive(true);
            _statusText.text = "Initializing...";

            _initialization = Addressables.InitializeAsync(false);

            try
            {
                yield return _initialization;

                if (_initialization.Status != AsyncOperationStatus.Succeeded)
                {
                    ShowError("Initialization failed", _initialization.OperationException);

                    yield break;
                }
            }
            finally
            {
                if (_initialization.IsValid())
                {
                    Addressables.Release(_initialization);

                    _initialization = default;
                }
            }

            _statusText.text = "Loading clock...";

            _sceneHandle = Addressables.LoadSceneAsync(_gameSceneAddress, LoadSceneMode.Additive, true);

            yield return _sceneHandle;

            if (_sceneHandle.Status != AsyncOperationStatus.Succeeded)
            {
                ShowError("Scene loading failed", _sceneHandle.OperationException);

                Addressables.Release(_sceneHandle);
                _sceneHandle = default;

                yield break;
            }

            SceneManager.SetActiveScene(_sceneHandle.Result.Scene);

            _bootCamera.enabled = false;
            _loadingScreen.SetActive(false);
        }

        private void ShowError(string message, System.Exception exception)
        {
            _statusText.text = message + ". Please restart the application.";
            Debug.LogError($"{message} ({_gameSceneAddress}): {exception}", this);
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void OnDestroy()
        {
            if (_isQuitting)
            {
                return;
            }

            if (_initialization.IsValid())
            {
                Addressables.Release(_initialization);
                _initialization = default;
            }

            if (!_sceneHandle.IsValid())
            {
                return;
            }

            if (_sceneHandle.IsDone)
            {
                ReleaseScene(_sceneHandle);
            }
            else
            {
                _sceneHandle.Completed += ReleaseScene;
            }
        }

        private void ReleaseScene(AsyncOperationHandle<SceneInstance> handle)
        {
            if (!handle.IsValid())
            {
                return;
            }

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                Addressables.UnloadSceneAsync(handle);
            }
            else
            {
                Addressables.Release(handle);
            }
        }
    }
}
