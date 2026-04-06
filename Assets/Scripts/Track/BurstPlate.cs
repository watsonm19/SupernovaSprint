// ═════════════════════════════════════════════════════════════════════════════
//  BurstPlate.cs
//  A trigger panel that gives the player a 20 m/s speed boost when they toggle
//  into Rocket Mode while standing on it in Normal Mode.
//
//  SETUP:
//    1. Add this script to a GameObject with a Trigger Collider.
//    2. Set the collider to isTrigger = true and size it to match the panel.
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;

public class BurstPlate : MonoBehaviour
{
    [Tooltip("Speed boost applied when the player switches to Rocket Mode on the plate (m/s).")]
    public float boostSpeed = 20f;

    private SupernovaSprintController _controller;
    private bool _playerOnPlate;

    private void OnTriggerEnter(Collider other)
    {
        var controller = other.GetComponent<SupernovaSprintController>()
                      ?? other.GetComponentInParent<SupernovaSprintController>();
        if (controller == null) return;

        _controller   = controller;
        _playerOnPlate = true;
        _controller.OnRocketToggle += OnRocketToggle;
    }

    private void OnTriggerExit(Collider other)
    {
        var controller = other.GetComponent<SupernovaSprintController>()
                      ?? other.GetComponentInParent<SupernovaSprintController>();
        if (controller == null || controller != _controller) return;

        _playerOnPlate = false;
        _controller.OnRocketToggle -= OnRocketToggle;
        _controller = null;
    }

    private void OnRocketToggle(bool isRocketMode)
    {
        if (!_playerOnPlate || !isRocketMode) return;

        // Boost along current velocity direction, or forward if nearly stationary
        var rb = _controller.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 dir = rb.linearVelocity.sqrMagnitude > 0.1f
            ? rb.linearVelocity.normalized
            : _controller.transform.forward;

        rb.linearVelocity += dir * boostSpeed;
    }
}
