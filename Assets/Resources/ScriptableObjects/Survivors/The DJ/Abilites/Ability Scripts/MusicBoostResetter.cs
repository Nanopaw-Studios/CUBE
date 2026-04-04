using System.Collections;
using UnityEngine;

/// <summary>
/// Attached temporarily to a survivor to restore their speed after the Music Circle boost expires.
/// Cancelled early if they exit the circle before the duration is up.
/// </summary>
public class MusicBoostResetter : MonoBehaviour
{
    private Coroutine resetCoroutine;

    /// <summary>
    /// Schedules a speed reset to <paramref name="baseSpeed"/> after <paramref name="delay"/> seconds.
    /// Replaces any previously scheduled reset.
    /// </summary>
    public void ScheduleReset(float baseSpeed, float delay)
    {
        if (resetCoroutine != null)
            StopCoroutine(resetCoroutine);

        resetCoroutine = StartCoroutine(ResetAfterDelay(baseSpeed, delay));
    }

    /// <summary>
    /// Cancels a pending reset (e.g., when the player exits the circle early).
    /// </summary>
    public void CancelReset()
    {
        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
            resetCoroutine = null;
        }
    }

    private IEnumerator ResetAfterDelay(float baseSpeed, float delay)
    {
        yield return new WaitForSeconds(delay);

        MovementSC movement = GetComponentInChildren<MovementSC>();
        if (movement != null)
            movement.Force = baseSpeed;

        resetCoroutine = null;
    }
}
