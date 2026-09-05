// Copyright (c) 2026 John B. Shull
// FuzzPhyte LLC is a company associated with John B. Shull
// This file is part of FP_Utility Package.
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md COMMERCIAL-LICENSE.md, and NOTICE.md.

namespace FuzzPhyte.Utility.LabelDisplay
{
    using UnityEngine;

    public class VRDirectionIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] public Transform actionTarget;
        [SerializeField] private Transform arrow; // must be a child of this transform

        [Header("Position")]
        [SerializeField] private float distance = 2f;
        [SerializeField] private float smoothTime = 0.25f;

        [Header("Arrow")]
        [SerializeField] private float arrowRadius = 0.5f;
        [SerializeField] private float arrowRotationOffset = -90f; // assumes arrow art points +Y by default
        [SerializeField] private float arrowSmoothSpeed = 10f;     // higher = snappier follow, lower = floatier

        [Header("Fading")]
        [Header("Visibility")]
        [SerializeField] private SpriteRenderer eyeRenderer;
        [SerializeField] private SpriteRenderer arrowRenderer;

        [SerializeField] private float fadeStartAngle = 5f;
        [SerializeField] private float fadeEndAngle = 15f;
        [SerializeField] private float fadeSpeed = 8f;

        private Vector3 positionVelocity;
        private float currentArrowAngle;

        private void Update()
        {
            if (actionTarget == null)
            {
                SetAlpha(eyeRenderer, 0);
                SetAlpha(arrowRenderer, 0);
                return;
            }

            UpdateFacingAndPosition();
            UpdateArrow();
            UpdateVisibility();
        }

        private void UpdateFacingAndPosition()
        {
            // Floating target position in front of the player
            Vector3 targetPosition = playerCamera.position + playerCamera.forward * distance;

            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref positionVelocity, smoothTime);

            // Face the player on Z, staying level with world up so head-tilt
            // doesn't roll the sprite. Fall back to camera.up only in the rare
            // colinear case (looking straight up/down at it).
            Vector3 directionToPlayer = playerCamera.position - transform.position;

            if (directionToPlayer.sqrMagnitude > 0.0001f)
            {
                Vector3 upReference = Vector3.up;

                if (Mathf.Abs(Vector3.Dot(directionToPlayer.normalized, upReference)) > 0.999f)
                    upReference = playerCamera.up;

                transform.rotation = Quaternion.LookRotation(directionToPlayer, upReference);
            }
        }

        private void UpdateArrow()
        {
            if (actionTarget == null || arrow == null) return;

            // Where the target sits on the indicator's local XY face
            Vector3 localTarget = transform.InverseTransformPoint(actionTarget.position);
            Vector2 direction = new Vector2(localTarget.x, localTarget.y);

            if (direction.sqrMagnitude < 0.0001f)
                return; // target dead-on behind/in front - hold last position

            direction.Normalize();
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Smoothly sweep to the new angle via the shortest path instead of
            // snapping every frame (LerpAngle handles the +-180 wraparound correctly)
            currentArrowAngle = Mathf.LerpAngle(currentArrowAngle, targetAngle, 1f - Mathf.Exp(-arrowSmoothSpeed * Time.deltaTime));

            float rad = currentArrowAngle * Mathf.Deg2Rad;
            Vector2 arrowDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            // Revolve around the sprite; staying purely on local X/Y keeps the
            // arrow's Z axis parallel to the sprite's (both facing the player)
            arrow.localPosition = new Vector3(arrowDir.x * arrowRadius, arrowDir.y * arrowRadius, arrow.localPosition.z);

            arrow.localRotation = Quaternion.Euler(0f, 0f, currentArrowAngle + arrowRotationOffset);
        }

        private void UpdateVisibility()
        {
            if (playerCamera == null || actionTarget == null)
                return;

            Vector3 toTarget = actionTarget.position - playerCamera.position;

            if (toTarget.sqrMagnitude < 0.0001f)
                return;

            toTarget.Normalize();

            float angle = Vector3.Angle(playerCamera.forward, toTarget);

            // 0 when looking directly at target,
            // 1 when looking farther away.
            float targetAlpha = Mathf.InverseLerp(fadeStartAngle, fadeEndAngle, angle);

            float currentAlpha = eyeRenderer.color.a;

            float alpha = Mathf.Lerp(currentAlpha, targetAlpha, 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime));

            SetAlpha(eyeRenderer, alpha);
            SetAlpha(arrowRenderer, alpha);
        }

        private void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null)
                return;

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }
}