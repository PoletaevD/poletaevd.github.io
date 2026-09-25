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

            ApplyAngles(Vector2.zero);
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

                _angleOffset = new Vector2(Mathf.DeltaAngle(angles.x, _hourHand.localEulerAngles.z), Mathf.DeltaAngle(angles.y, _minuteHand.localEulerAngles.z));

                _correction = DOTween.To(() => _angleOffset, value => _angleOffset = value, Vector2.zero, 0.35f).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            ApplyAngles(angles + _angleOffset);

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

        private Vector2 GetAngles(TimeSpan timeOfDay)
        {
            var seconds = timeOfDay.TotalSeconds;

            return new Vector2((float)(-seconds / 120d % 360d), (float)(-seconds / 10d % 360d));
        }

        private void ApplyAngles(Vector2 angles)
        {
            _hourHand.localRotation = Quaternion.Euler(0f, 0f, angles.x);
            _minuteHand.localRotation = Quaternion.Euler(0f, 0f, angles.y);
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
