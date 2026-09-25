using System;
using System.Collections;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Clock.TimeSync
{
    public sealed class TimeApiService
    {
        private const string DefaultEndpoint = "https://timeapi.io/api/Time/current/zone?timeZone=UTC";

        public enum Failure
        {
            Network,
            Timeout,
            InvalidResponse
        }

        private readonly string _endpoint;

        public TimeApiService(string endpoint = DefaultEndpoint)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new ArgumentException("An absolute HTTPS endpoint without a fragment is required.", nameof(endpoint));
            }

            _endpoint = endpoint;
        }

        public IEnumerator FetchUtc(Action<DateTimeOffset> onSuccess, Action<Failure, string> onFailure, int timeoutSeconds = 10)
        {
            if (onSuccess == null)
            {
                throw new ArgumentNullException(nameof(onSuccess));
            }

            if (onFailure == null)
            {
                throw new ArgumentNullException(nameof(onFailure));
            }

            if (timeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            }

            return FetchUtcRoutine(onSuccess, onFailure, timeoutSeconds);
        }

        private IEnumerator FetchUtcRoutine(Action<DateTimeOffset> onSuccess, Action<Failure, string> onFailure, int timeoutSeconds)
        {
            var separator = _endpoint.Contains("?") ? "&" : "?";
            var url = _endpoint + separator + "_=" + Guid.NewGuid().ToString("N");

            using (var request = UnityWebRequest.Get(url))
            {
                var startedAt = Time.realtimeSinceStartupAsDouble;
                string startError = null;

                try
                {
                    request.SendWebRequest();
                }
                catch (InvalidOperationException exception)
                {
                    startError = exception.Message;
                }

                if (startError != null)
                {
                    onFailure(Failure.Network, startError);

                    yield break;
                }

                while (!request.isDone)
                {
                    if (Time.realtimeSinceStartupAsDouble - startedAt >= timeoutSeconds)
                    {
                        request.Abort();
                        onFailure(Failure.Timeout, "Time synchronization timed out.");

                        yield break;
                    }

                    yield return null;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onFailure(Failure.Network, $"Time synchronization failed (HTTP {request.responseCode}): {request.error}");

                    yield break;
                }

                if (!TryParseUtc(request.downloadHandler.text, out var utcTime))
                {
                    onFailure(Failure.InvalidResponse, "Expected a valid TimeAPI.io UTC response.");

                    yield break;
                }

                onSuccess(utcTime);
            }
        }

        private bool TryParseUtc(string json, out DateTimeOffset utcTime)
        {
            utcTime = default;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                var response = JObject.Parse(json, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });

                if (!TryReadInteger(response, "year", out var year) ||
                    !TryReadInteger(response, "month", out var month) ||
                    !TryReadInteger(response, "day", out var day) ||
                    !TryReadInteger(response, "hour", out var hour) ||
                    !TryReadInteger(response, "minute", out var minute) ||
                    !TryReadInteger(response, "seconds", out var second) ||
                    !TryReadInteger(response, "milliSeconds", out var millisecond) ||
                    millisecond < 0 || millisecond > 999 ||
                    response["timeZone"]?.Type != JTokenType.String ||
                    !string.Equals(response["timeZone"].ToString(), "UTC", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                utcTime = new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero).AddMilliseconds(millisecond);

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private bool TryReadInteger(JObject response, string propertyName, out int value)
        {
            value = default;
            var token = response[propertyName];

            return token != null && token.Type == JTokenType.Integer && int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
