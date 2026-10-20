using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UnityConsent;

// The single entry point for gameplay analytics. Gameplay code only calls
// Record(...); this class owns Unity Gaming Services start-up, so the backend
// can change without touching GameManager. Events recorded before the
// service is ready are held and sent once it starts.
//
// Every event name and parameter used here must also exist in the UGS
// dashboard's Event Manager, or the service rejects the event. See
// ANALYTICS.md for the full list.
public static class GameAnalytics
{
    private const string ConsentKey = "analytics_consent";

    private static readonly List<CustomEvent> pending = new List<CustomEvent>();
    private static readonly Dictionary<string, int> attemptsByLevel = new Dictionary<string, int>();
    private static bool ready;
    private static bool failed;

    // Playtesters can opt out; the choice persists between sessions.
    public static bool HasConsent
    {
        get => PlayerPrefs.GetInt(ConsentKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(ConsentKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ApplyConsent(value);
        }
    }

    // UGS starts and stops data collection from Unity's consent state.
    private static void ApplyConsent(bool granted)
    {
        ConsentState state = EndUserConsent.GetConsentState();
        state.AnalyticsIntent = granted ? ConsentStatus.Granted : ConsentStatus.Denied;
        EndUserConsent.SetConsentState(state);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState()
    {
        // Static state survives Enter Play Mode when domain reload is off.
        pending.Clear();
        attemptsByLevel.Clear();
        ready = false;
        failed = false;
        Initialize();
    }

    private static async void Initialize()
    {
        try
        {
            // Granting consent before initializing lets the service start
            // collecting as soon as it is up.
            ApplyConsent(HasConsent);
            await UnityServices.InitializeAsync();
            ready = true;

            foreach (CustomEvent queued in pending)
            {
                AnalyticsService.Instance.RecordEvent(queued);
            }
        }
        catch (Exception exception)
        {
            // Analytics must never break the game.
            failed = true;
            Debug.LogWarning($"Analytics disabled: {exception.Message}");
        }

        pending.Clear();
    }

    // Counts each start of a level during this session, including restarts.
    public static int NextAttempt(string levelName)
    {
        attemptsByLevel.TryGetValue(levelName, out int attempts);
        attempts++;
        attemptsByLevel[levelName] = attempts;
        return attempts;
    }

    // Values must be string, int, float, or bool.
    public static void Record(string eventName, params (string key, object value)[] parameters)
    {
        if (failed || !HasConsent)
        {
            return;
        }

        CustomEvent customEvent = new CustomEvent(eventName);
        foreach ((string key, object value) in parameters)
        {
            if (value != null)
            {
                customEvent.Add(key, value);
            }
        }

        if (ready)
        {
            AnalyticsService.Instance.RecordEvent(customEvent);
        }
        else
        {
            pending.Add(customEvent);
        }
    }

    // Sends queued events now instead of waiting for the next upload. Useful
    // on WebGL, where a closed tab can drop anything still queued.
    public static void Flush()
    {
        if (ready && HasConsent)
        {
            AnalyticsService.Instance.Flush();
        }
    }
}
