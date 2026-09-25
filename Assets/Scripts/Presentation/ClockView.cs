using System;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using Zenject;

namespace Clock.Presentation
{
    public sealed class ClockView : MonoBehaviour
    {
        [SerializeField] private RectTransform _hourHand;
        [SerializeField] private RectTransform _minuteHand;
        [SerializeField] private RectTransform _secondHand;
        [SerializeField] private TMP_Text _digitalTime;
        [SerializeField] private CanvasGroup _content;

        private IClockTimeSource _timeSource;
        private Tween _reveal;
        private Tween _correction;
        private Vector2 _angleOffset;
        private DateTime _previousTime;
        private double _previousRealtime;
        private long _displayedSecond = -1;
        private bool _hasSample;
        private bool _isPreview;
        private bool _revealed;

        [Inject]
        private void Construct(IClockTimeSource timeSource)
        {
            _timeSource = timeSource;
        }

        private void OnEnable()
        {
            _hasSample = false;
            _displayedSecond = -1;
            _revealed = false;
            _content.alpha = 0.6f;
            _digitalTime.text = "--:--:--";

            ApplyAngles(Vector3.zero);
        }

        private void Update()
        {
            if (_timeSource == null || !_timeSource.IsReady || _isPreview)
            {
                return;
            }

            if (!_revealed)
            {
                _revealed = true;
                _reveal = _content.DOFade(1f, 0.4f).SetEase(Ease.OutQuad).SetUpdate(true);
            }

            var now = _timeSource.Now;
            var realtime = Time.realtimeSinceStartupAsDouble;
            var angles = GetAngles(now.TimeOfDay);

            if (_hasSample && Math.Abs((now - _previousTime).TotalSeconds - (realtime - _previousRealtime)) > 0.5d)
            {
                _correction?.Kill();

                _angleOffset.x = Mathf.DeltaAngle(angles.x, _hourHand.localEulerAngles.z);
                _angleOffset.y = Mathf.DeltaAngle(angles.y, _minuteHand.localEulerAngles.z);

                _correction = DOTween.To(() => _angleOffset, value => _angleOffset = value, Vector2.zero, 0.35f).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            ApplyAngles(new Vector3(angles.x + _angleOffset.x, angles.y + _angleOffset.y, angles.z));

            var wholeSecond = now.Ticks / TimeSpan.TicksPerSecond;

            if (_displayedSecond != wholeSecond)
            {
                _digitalTime.text = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                _displayedSecond = wholeSecond;
            }

            _previousTime = now;
            _previousRealtime = realtime;
            _hasSample = true;
        }

        public void ShowPreview(TimeSpan timeOfDay)
        {
            _isPreview = true;
            _correction?.Kill();
            _angleOffset = Vector2.zero;

            ApplyAngles(GetAngles(timeOfDay));

            _digitalTime.text = timeOfDay.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        }

        public void EndPreview()
        {
            _isPreview = false;
            _hasSample = false;

            _displayedSecond = -1;
            _angleOffset = Vector2.zero;
        }

        private Vector3 GetAngles(TimeSpan timeOfDay)
        {
            var seconds = timeOfDay.TotalSeconds;
            var hourAngle = (float)(-seconds / 120d % 360d);
            var minuteAngle = (float)(-seconds / 10d % 360d);
            var secondAngle = -timeOfDay.Seconds * 6f;

            return new Vector3(hourAngle, minuteAngle, secondAngle);
        }

        private void ApplyAngles(Vector3 angles)
        {
            _hourHand.localRotation = Quaternion.Euler(0f, 0f, angles.x);
            _minuteHand.localRotation = Quaternion.Euler(0f, 0f, angles.y);
            _secondHand.localRotation = Quaternion.Euler(0f, 0f, angles.z);
        }

        private void OnDisable()
        {
            _reveal?.Kill();
            _correction?.Kill();
            _angleOffset = Vector2.zero;
            _hasSample = false;
            _isPreview = false;
        }
    }
}
