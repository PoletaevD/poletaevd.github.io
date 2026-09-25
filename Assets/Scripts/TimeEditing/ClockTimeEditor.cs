using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Clock.TimeEditing
{
    public interface IEditableClock
    {
        TimeSpan TimeOfDay { get; }
        void SetTimeAndResume(TimeSpan timeOfDay);
    }

    [DisallowMultipleComponent]
    public sealed class ClockTimeEditor : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _hoursInput;
        [SerializeField] private TMP_InputField _minutesInput;
        [SerializeField] private UnityEngine.UI.Button _saveButton;
        [SerializeField] private TMP_Text _errorText;

        private IEditableClock _clock;

        public bool IsEditing { get; private set; }
        public event Action Saved;

        public void Bind(IEditableClock clock)
        {
            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            CancelEdit();

            _clock = clock;
        }

        public void BeginEdit()
        {
            if (!isActiveAndEnabled || _clock == null || !HasFields())
            {
                SetError("Редактор времени не подключён.");

                return;
            }

            var time = _clock.TimeOfDay;

            ConfigureInput(_hoursInput);
            ConfigureInput(_minutesInput);

            _hoursInput.SetTextWithoutNotify(time.Hours.ToString("D2", CultureInfo.InvariantCulture));
            _minutesInput.SetTextWithoutNotify(time.Minutes.ToString("D2", CultureInfo.InvariantCulture));

            IsEditing = true;

            SetInteractable(true);
            SetError(string.Empty);

            _hoursInput.Select();
            _hoursInput.ActivateInputField();
        }

        public void Save()
        {
            if (!IsEditing || _clock == null || !HasFields())
            {
                return;
            }

            if (!TryParseTime(_hoursInput.text, _minutesInput.text, out var time))
            {
                SetError("Введите часы от 00 до 23 и минуты от 00 до 59.");

                return;
            }

            _clock.SetTimeAndResume(time);

            CancelEdit();

            Saved?.Invoke();
        }

        public void CancelEdit()
        {
            IsEditing = false;

            SetInteractable(false);
            SetError(string.Empty);
        }

        public bool TryParseTime(string hours, string minutes, out TimeSpan time)
        {
            time = default;

            if (!TryParsePart(hours, 23, out var hour) || !TryParsePart(minutes, 59, out var minute))
            {
                return false;
            }

            time = new TimeSpan(hour, minute, 0);

            return true;
        }

        private void OnEnable()
        {
            if (_saveButton != null)
            {
                _saveButton.onClick.AddListener(Save);
            }

            CancelEdit();
        }

        private void OnDisable()
        {
            if (_saveButton != null)
            {
                _saveButton.onClick.RemoveListener(Save);
            }

            CancelEdit();
        }

        private bool HasFields()
        {
            return _hoursInput != null && _minutesInput != null && _hoursInput != _minutesInput;
        }

        private void ConfigureInput(TMP_InputField input)
        {
            input.contentType = TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterValidation = TMP_InputField.CharacterValidation.None;
            input.characterLimit = 0;
            input.readOnly = false;
        }

        private bool TryParsePart(string text, int maximum, out int value)
        {
            value = 0;

            if (string.IsNullOrEmpty(text) || text.Length > 2)
            {
                return false;
            }

            foreach (var character in text)
            {
                if (character < '0' || character > '9')
                {
                    return false;
                }

                value = value * 10 + character - '0';
            }

            return value <= maximum;
        }

        private void SetInteractable(bool interactable)
        {
            if (_hoursInput != null)
            {
                _hoursInput.interactable = interactable;
            }

            if (_minutesInput != null)
            {
                _minutesInput.interactable = interactable;
            }

            if (_saveButton != null)
            {
                _saveButton.interactable = interactable;
            }
        }

        private void SetError(string message)
        {
            if (_errorText != null)
            {
                _errorText.text = message;
            }
        }
    }
}
