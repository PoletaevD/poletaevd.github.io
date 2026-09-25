using System;
using Clock.Editing;
using Clock.TimeEditing;
using Clock.TimeSync;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using DragEditor = Clock.Editing.ClockTimeEditor;
using KeyboardEditor = Clock.TimeEditing.ClockTimeEditor;

namespace Clock.Presentation
{
    public sealed class ClockController : MonoBehaviour, IClockTimeSource, IClockEditModel, IEditableClock
    {
        private enum EditMode
        {
            None,
            Hands,
            Keyboard
        }

        [SerializeField] private ClockView _view;
        [SerializeField] private DragEditor _dragEditor;
        [SerializeField] private KeyboardEditor _keyboardEditor;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _handsButton;
        [SerializeField] private Button _keyboardButton;
        [SerializeField] private Button _saveHandsButton;
        [SerializeField] private Button _cancelHandsButton;
        [SerializeField] private Button _cancelKeyboardButton;
        [SerializeField] private GameObject _modeButtons;
        [SerializeField] private GameObject _handsButtons;
        [SerializeField] private GameObject _keyboardPanel;

        private ServerClock _clock;
        private TimeApiService _timeService;
        private DateTimeOffset _pausedUtc;
        private EditMode _editMode;
        private bool _isPaused;
        private bool _isFetching;

        public bool IsReady => _clock != null && _clock.IsInitialized;
        public DateTime Now => CurrentLocalTime.DateTime;
        public TimeSpan TimeOfDay => CurrentLocalTime.TimeOfDay;

        private DateTimeOffset CurrentLocalTime => (_isPaused ? _pausedUtc : _clock.UtcNow).ToLocalTime();

        [Inject]
        private void Construct(ServerClock clock, TimeApiService timeService)
        {
            _clock = clock;
            _timeService = timeService;
        }

        private void Start()
        {
            _dragEditor.Bind(this);
            _keyboardEditor.Bind(this);

            _dragEditor.PreviewChanged += OnPreviewChanged;
            _dragEditor.EditingChanged += OnHandsEditingChanged;
            _keyboardEditor.Saved += OnKeyboardSaved;
            _retryButton.onClick.AddListener(RequestTime);
            _handsButton.onClick.AddListener(BeginHandsEdit);
            _keyboardButton.onClick.AddListener(BeginKeyboardEdit);
            _saveHandsButton.onClick.AddListener(_dragEditor.Save);
            _cancelHandsButton.onClick.AddListener(_dragEditor.Cancel);
            _cancelKeyboardButton.onClick.AddListener(CancelKeyboardEdit);

            _modeButtons.SetActive(false);
            _handsButtons.SetActive(false);
            _keyboardPanel.SetActive(false);

            RequestTime();
        }

        private void RequestTime()
        {
            if (_isFetching)
            {
                return;
            }

            _isFetching = true;
            _retryButton.gameObject.SetActive(false);
            _statusText.text = "Получаем серверное время...";

            StartCoroutine(_timeService.FetchUtc(
                utc =>
                {
                    _clock.SetUtcTime(utc);
                    _isFetching = false;
                    _statusText.text = "Синхронизировано с сервером";
                    _modeButtons.SetActive(true);
                },
                (failure, message) =>
                {
                    _isFetching = false;
                    _statusText.text = "Не удалось получить время: " + message;
                    _retryButton.gameObject.SetActive(true);
                    Debug.LogWarning("Clock synchronization failed: " + failure + ": " + message, this);
                }, 20));
        }

        private void BeginHandsEdit()
        {
            if (_editMode != EditMode.None || !IsReady)
            {
                return;
            }

            _dragEditor.BeginEdit();
            if (!_dragEditor.IsEditing)
            {
                return;
            }

            _editMode = EditMode.Hands;

            _modeButtons.SetActive(false);
            _handsButtons.SetActive(true);

            _statusText.text = "Перетащите часовую или минутную стрелку";
        }

        private void BeginKeyboardEdit()
        {
            if (_editMode != EditMode.None || !IsReady)
            {
                return;
            }

            PauseAndGetTime();

            _editMode = EditMode.Keyboard;

            _modeButtons.SetActive(false);
            _keyboardPanel.SetActive(true);

            _view.ShowPreview(TimeOfDay);
            _keyboardEditor.BeginEdit();

            _statusText.text = "Введите новое время";
        }

        private void CancelKeyboardEdit()
        {
            if (_editMode != EditMode.Keyboard)
            {
                return;
            }

            _keyboardEditor.CancelEdit();

            Resume();
            CloseEditMode();
        }

        private void OnKeyboardSaved()
        {
            if (_editMode == EditMode.Keyboard)
            {
                CloseEditMode();
            }
        }

        private void OnHandsEditingChanged(bool isEditing)
        {
            if (!isEditing && _editMode == EditMode.Hands)
            {
                CloseEditMode();
            }
        }

        private void OnPreviewChanged(TimeSpan timeOfDay)
        {
            _view.ShowPreview(timeOfDay);
        }

        private void CloseEditMode()
        {
            _editMode = EditMode.None;

            _handsButtons.SetActive(false);
            _keyboardPanel.SetActive(false);
            _modeButtons.SetActive(true);

            _statusText.text = "Часы продолжают ход";
        }

        public TimeSpan PauseAndGetTime()
        {
            if (!_isPaused)
            {
                _pausedUtc = _clock.UtcNow;
                _isPaused = true;
            }

            return _pausedUtc.ToLocalTime().TimeOfDay;
        }

        public void SetTimeAndResume(TimeSpan timeOfDay)
        {
            if (timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1))
            {
                throw new ArgumentOutOfRangeException(nameof(timeOfDay));
            }

            var local = CurrentLocalTime;
            var changed = new DateTimeOffset(local.Year, local.Month, local.Day,
                timeOfDay.Hours, timeOfDay.Minutes, timeOfDay.Seconds, local.Offset);

            _clock.SetUtcTime(changed.ToUniversalTime());

            _isPaused = false;

            _view.EndPreview();
        }

        public void Resume()
        {
            if (_isPaused)
            {
                _clock.SetUtcTime(_pausedUtc);
                _isPaused = false;
            }

            _view.EndPreview();
        }

        private void OnDestroy()
        {
            _dragEditor.PreviewChanged -= OnPreviewChanged;
            _dragEditor.EditingChanged -= OnHandsEditingChanged;

            _keyboardEditor.Saved -= OnKeyboardSaved;

            _retryButton.onClick.RemoveListener(RequestTime);
            _handsButton.onClick.RemoveListener(BeginHandsEdit);
            _keyboardButton.onClick.RemoveListener(BeginKeyboardEdit);
            _saveHandsButton.onClick.RemoveListener(_dragEditor.Save);
            _cancelHandsButton.onClick.RemoveListener(_dragEditor.Cancel);
            _cancelKeyboardButton.onClick.RemoveListener(CancelKeyboardEdit);
        }
    }
}
