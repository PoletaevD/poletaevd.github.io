using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Clock.Editing
{
    public interface IClockEditModel
    {
        TimeSpan PauseAndGetTime();
        void SetTimeAndResume(TimeSpan timeOfDay);
        void Resume();
    }

    public sealed class ClockTimeEditor : MonoBehaviour, IInitializePotentialDragHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const double SecondsPerDay = 86400d;

        [SerializeField] private RectTransform _dial;
        [SerializeField] private RectTransform _hourHand;
        [SerializeField] private RectTransform _minuteHand;
        [SerializeField, Min(0f)] private float _centerDeadZone = 8f;

        private IClockEditModel _model;
        private RectTransform _draggedHand;
        private Camera _dragCamera;
        private int _pointerId;
        private float _lastAngle;
        private bool _hasAngle;
        private double _draftSeconds;

        public bool IsEditing { get; private set; }
        public TimeSpan PreviewTime => TimeSpan.FromSeconds(_draftSeconds);

        public event Action<bool> EditingChanged;
        public event Action<TimeSpan> PreviewChanged;

        public void Bind(IClockEditModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (IsEditing)
            {
                throw new InvalidOperationException("Cannot replace the model during editing.");
            }

            _model = model;
        }

        public void BeginEdit()
        {
            if (IsEditing || !isActiveAndEnabled)
            {
                return;
            }

            if (_model == null || _dial == null || _hourHand == null || _minuteHand == null)
            {
                Debug.LogError("ClockTimeEditor requires a model, dial, and both hands.", this);

                return;
            }

            _hourHand.GetComponent<UnityEngine.UI.Image>().alphaHitTestMinimumThreshold = 0.1f;
            _minuteHand.GetComponent<UnityEngine.UI.Image>().alphaHitTestMinimumThreshold = 0.1f;
            _draftSeconds = NormalizeSeconds(_model.PauseAndGetTime().TotalSeconds);

            IsEditing = true;

            EditingChanged?.Invoke(true);
            PreviewChanged?.Invoke(PreviewTime);
        }

        public void Save()
        {
            if (!IsEditing)
            {
                return;
            }

            var time = PreviewTime;

            StopDragging();

            _model.SetTimeAndResume(time);

            IsEditing = false;

            EditingChanged?.Invoke(false);
        }

        public void Cancel()
        {
            if (!IsEditing)
            {
                return;
            }

            StopDragging();

            _model.Resume();

            IsEditing = false;

            EditingChanged?.Invoke(false);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (IsEditing)
            {
                eventData.useDragThreshold = false;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsEditing || _draggedHand != null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            var target = eventData.pointerPressRaycast.gameObject;

            if (target == null)
            {
                return;
            }

            var hit = target.transform;

            _draggedHand = hit.IsChildOf(_minuteHand) ? _minuteHand
                : hit.IsChildOf(_hourHand) ? _hourHand : null;

            if (_draggedHand == null)
            {
                return;
            }

            _pointerId = eventData.pointerId;
            _dragCamera = eventData.pressEventCamera;

            _hasAngle = TryGetAngle(eventData.pressPosition, out _lastAngle);

            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsEditing || _draggedHand == null || eventData.pointerId != _pointerId)
            {
                return;
            }

            if (!TryGetAngle(eventData.position, out var angle))
            {
                _hasAngle = false;

                return;
            }

            if (_hasAngle)
            {
                var delta = Mathf.DeltaAngle(_lastAngle, angle);
                var secondsPerDegree = _draggedHand == _minuteHand ? 10d : 120d;

                _draftSeconds = NormalizeSeconds(_draftSeconds + delta * secondsPerDegree);

                PreviewChanged?.Invoke(PreviewTime);
            }

            _lastAngle = angle;
            _hasAngle = true;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_draggedHand != null && eventData.pointerId == _pointerId)
            {
                OnDrag(eventData);

                StopDragging();
            }
        }

        private bool TryGetAngle(Vector2 screenPosition, out float angle)
        {
            angle = 0f;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_dial, screenPosition, _dragCamera, out var localPosition))
            {
                return false;
            }

            var offset = localPosition - _dial.rect.center;

            if (offset.sqrMagnitude <= _centerDeadZone * _centerDeadZone)
            {
                return false;
            }

            angle = Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;

            return true;
        }

        private double NormalizeSeconds(double seconds)
        {
            return (seconds % SecondsPerDay + SecondsPerDay) % SecondsPerDay;
        }

        private void StopDragging()
        {
            _draggedHand = null;
            _dragCamera = null;
            _hasAngle = false;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                StopDragging();
            }
        }

        private void OnDisable()
        {
            Cancel();
        }
    }
}
