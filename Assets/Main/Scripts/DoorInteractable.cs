using UnityEngine;

public class DoorInteractable : InteractableBase
{
    [Header("Door Setup")]
    [SerializeField] private Transform doorVisual;
    [SerializeField] private Transform doorPivot;
    [SerializeField] private Collider interactionCollider;
    [SerializeField] private Collider[] additionalInteractionColliders;
    [SerializeField] private bool autoFindChildCollider = true;
    [SerializeField] private Vector3 closedLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 openLocalEuler = new Vector3(0f, 95f, 0f);
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private bool startsOpen;
    [SerializeField] private Transform handContactPoint;
    [SerializeField] private float handContactDuration = 0.35f;
    [SerializeField] private float handContactNormalOffset = 0.02f;
    [SerializeField] private bool holdHandOnHandleWhileInteractHeld = true;
    [SerializeField] private Vector3 handFollowLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 handFollowLocalEulerOffset = Vector3.zero;
    [SerializeField] private bool debugDoorLogs = true;
    [SerializeField] private bool debugDrawPivot = true;
    [SerializeField] private bool applyRotationInLateUpdate = true;

    private bool isOpen;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion basePivotRotation;
    private bool useExternalPivotMode;
    private Quaternion closedHingeWorldRotation;
    private Quaternion openHingeWorldRotation;
    private Quaternion currentHingeWorldRotation;
    private Vector3 closedPositionOffsetFromHinge;
    private Quaternion closedRotationOffsetFromHinge;
    private Animator visualAnimator;
    private Rigidbody pivotRigidbody;
    private bool loggedOverwriteWarning;
    private PlayerInteractor focusedInteractor;

    private void Awake()
    {
        if (doorVisual == null)
        {
            doorVisual = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }

        if (doorPivot == null)
        {
            doorPivot = doorVisual;
        }

        if (doorPivot == null)
        {
            doorPivot = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }

        useExternalPivotMode = doorVisual != null && doorPivot != null && !doorVisual.IsChildOf(doorPivot);
        isOpen = startsOpen;

        if (useExternalPivotMode)
        {
            Quaternion pivotBaseWorldRotation = doorPivot.rotation;
            closedHingeWorldRotation = pivotBaseWorldRotation * Quaternion.Euler(closedLocalEuler);
            openHingeWorldRotation = pivotBaseWorldRotation * Quaternion.Euler(openLocalEuler);

            // Treat authored visual transform as closed pose reference for external hinge setups.
            closedPositionOffsetFromHinge = Quaternion.Inverse(closedHingeWorldRotation) * (doorVisual.position - doorPivot.position);
            closedRotationOffsetFromHinge = Quaternion.Inverse(closedHingeWorldRotation) * doorVisual.rotation;

            currentHingeWorldRotation = isOpen ? openHingeWorldRotation : closedHingeWorldRotation;
            ApplyExternalVisualFromHinge(currentHingeWorldRotation);
        }
        else
        {
            basePivotRotation = doorPivot.localRotation;
            closedRotation = basePivotRotation * Quaternion.Euler(closedLocalEuler);
            openRotation = basePivotRotation * Quaternion.Euler(openLocalEuler);
            doorPivot.localRotation = isOpen ? openRotation : closedRotation;
        }

        visualAnimator = doorVisual != null ? doorVisual.GetComponentInChildren<Animator>() : null;
        pivotRigidbody = doorPivot.GetComponent<Rigidbody>();

        BindInteractionCollider();

        if (debugDoorLogs)
        {
            string visualName = doorVisual != null ? doorVisual.name : "null";
            Debug.Log($"{nameof(DoorInteractable)} '{name}' initialized. visual='{visualName}', pivot='{doorPivot.name}', externalPivotMode={useExternalPivotMode}, startsOpen={startsOpen}", this);

            if (visualAnimator != null && visualAnimator.enabled)
            {
                Debug.LogWarning($"{nameof(DoorInteractable)} '{name}' detected Animator on visual. Animator may override door rotation.", this);
            }

            if (pivotRigidbody != null && !pivotRigidbody.isKinematic)
            {
                Debug.LogWarning($"{nameof(DoorInteractable)} '{name}' has non-kinematic Rigidbody on pivot. Physics may override transform rotation.", this);
            }
        }
    }

    private void Update()
    {
        if (!applyRotationInLateUpdate)
        {
            ApplyDoorRotation(Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        if (applyRotationInLateUpdate)
        {
            ApplyDoorRotation(Time.deltaTime);
        }

        UpdateHeldHandFollow();
    }

    protected override void PerformInteract(PlayerInteractor interactor)
    {
        bool oldState = isOpen;
        isOpen = !isOpen;
        ApplyHandContact(interactor);

        if (debugDoorLogs)
        {
            string by = interactor != null ? interactor.name : "unknown";
            Debug.Log($"{nameof(DoorInteractable)} '{name}' interacted by '{by}'. {oldState} -> {isOpen}", this);
        }
    }

    public override void OnFocusEnter(PlayerInteractor interactor)
    {
        focusedInteractor = interactor;

        if (debugDoorLogs)
        {
            string by = interactor != null ? interactor.name : "unknown";
            Debug.Log($"{nameof(DoorInteractable)} '{name}' focus enter by '{by}'", this);
        }
    }

    public override void OnFocusExit(PlayerInteractor interactor)
    {
        if (focusedInteractor == interactor)
        {
            focusedInteractor?.StopRightHandFollowTarget();
            focusedInteractor = null;
        }

        if (debugDoorLogs)
        {
            string by = interactor != null ? interactor.name : "unknown";
            Debug.Log($"{nameof(DoorInteractable)} '{name}' focus exit by '{by}'", this);
        }
    }

    private void BindInteractionCollider()
    {
        if (interactionCollider == null && autoFindChildCollider)
        {
            if (doorVisual != null)
            {
                interactionCollider = doorVisual.GetComponentInChildren<Collider>();
            }

            if (interactionCollider == null && doorPivot != null)
            {
                interactionCollider = doorPivot.GetComponentInChildren<Collider>();
            }

            if (interactionCollider == null)
            {
                interactionCollider = GetComponentInChildren<Collider>();
            }
        }

        bool boundAny = false;

        if (interactionCollider != null)
        {
            BindCollider(interactionCollider);
            boundAny = true;
        }

        if (additionalInteractionColliders != null)
        {
            for (int i = 0; i < additionalInteractionColliders.Length; i++)
            {
                Collider collider = additionalInteractionColliders[i];
                if (collider == null)
                {
                    continue;
                }

                BindCollider(collider);
                boundAny = true;
            }
        }

        if (!boundAny)
        {
            Debug.LogWarning($"{nameof(DoorInteractable)} on '{name}' has no interaction collider assigned/found.", this);
        }
        else if (debugDoorLogs)
        {
            Debug.Log($"{nameof(DoorInteractable)} '{name}' bound interaction colliders.", this);
        }
    }

    private void BindCollider(Collider targetCollider)
    {
        InteractableTarget target = targetCollider.GetComponent<InteractableTarget>();
        if (target == null)
        {
            target = targetCollider.gameObject.AddComponent<InteractableTarget>();
        }
        target.SetInteractable(this);
    }

    private void ApplyHandContact(PlayerInteractor interactor)
    {
        if (interactor == null)
        {
            return;
        }

        Vector3 point;
        Vector3 normal;

        if (handContactPoint != null)
        {
            point = handContactPoint.position;
            normal = handContactPoint.forward;
        }
        else if (!interactor.TryGetCurrentTargetHit(out point, out normal))
        {
            return;
        }

        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : transform.forward;
        Vector3 handPos = point + safeNormal * handContactNormalOffset;
        Quaternion handRot = Quaternion.LookRotation(-safeNormal, Vector3.up);

        interactor.SetRightHandWorldOverride(handPos, handRot, handContactDuration);

        if (debugDoorLogs)
        {
            Debug.Log($"{nameof(DoorInteractable)} '{name}' applied hand contact at {handPos}", this);
        }
    }

    private void UpdateHeldHandFollow()
    {
        if (!holdHandOnHandleWhileInteractHeld || focusedInteractor == null)
        {
            return;
        }

        if (!focusedInteractor.IsInteractHeld || !ReferenceEquals(focusedInteractor.CurrentInteractable, this))
        {
            focusedInteractor.StopRightHandFollowTarget();
            return;
        }

        Transform followTarget = handContactPoint != null
            ? handContactPoint
            : (doorVisual != null ? doorVisual : doorPivot);

        if (followTarget == null)
        {
            return;
        }

        focusedInteractor.SetRightHandFollowTarget(followTarget, handFollowLocalOffset, handFollowLocalEulerOffset);

        // Force the right hand to the latest handle transform this frame so it tracks door motion tightly.
        Vector3 worldPos = followTarget.TransformPoint(handFollowLocalOffset);
        Quaternion worldRot = followTarget.rotation * Quaternion.Euler(handFollowLocalEulerOffset);
        focusedInteractor.ForceRightHandWorldPose(worldPos, worldRot);
    }

    private void ApplyDoorRotation(float deltaTime)
    {
        if (doorPivot == null)
        {
            return;
        }

        Quaternion target;
        Quaternion next;

        if (useExternalPivotMode)
        {
            if (doorVisual == null)
            {
                return;
            }

            target = isOpen ? openHingeWorldRotation : closedHingeWorldRotation;
            currentHingeWorldRotation = Quaternion.Slerp(currentHingeWorldRotation, target, deltaTime * rotationSpeed);
            next = currentHingeWorldRotation;
            ApplyExternalVisualFromHinge(currentHingeWorldRotation);
        }
        else
        {
            target = isOpen ? openRotation : closedRotation;
            next = Quaternion.Slerp(doorPivot.localRotation, target, deltaTime * rotationSpeed);
            doorPivot.localRotation = next;
        }

        if (!debugDoorLogs || loggedOverwriteWarning)
        {
            return;
        }

        // If we keep writing rotation but state never converges, something else may be overriding it.
        if (Quaternion.Angle(next, target) > 1f && Quaternion.Angle(doorPivot.localRotation, target) > 1f && Time.frameCount % 120 == 0)
        {
            loggedOverwriteWarning = true;
            Debug.LogWarning(
                $"{nameof(DoorInteractable)} '{name}' is not converging to target rotation. Possible override by Animator/physics/other script or wrong pivot axis.",
                this
            );
        }
    }

    private void ApplyExternalVisualFromHinge(Quaternion hingeWorldRotation)
    {
        doorVisual.position = doorPivot.position + hingeWorldRotation * closedPositionOffsetFromHinge;
        doorVisual.rotation = hingeWorldRotation * closedRotationOffsetFromHinge;
    }

    [ContextMenu("Debug Toggle Door")]
    private void DebugToggleDoor()
    {
        PerformInteract(null);
    }

    [ContextMenu("Debug Snap Open")]
    private void DebugSnapOpen()
    {
        isOpen = true;
        if (useExternalPivotMode)
        {
            currentHingeWorldRotation = openHingeWorldRotation;
            ApplyExternalVisualFromHinge(currentHingeWorldRotation);
        }
        else if (doorPivot != null)
        {
            doorPivot.localRotation = openRotation;
        }
    }

    [ContextMenu("Debug Snap Closed")]
    private void DebugSnapClosed()
    {
        isOpen = false;
        if (useExternalPivotMode)
        {
            currentHingeWorldRotation = closedHingeWorldRotation;
            ApplyExternalVisualFromHinge(currentHingeWorldRotation);
        }
        else if (doorPivot != null)
        {
            doorPivot.localRotation = closedRotation;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDrawPivot || doorPivot == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(doorPivot.position, doorPivot.position + doorPivot.forward * 0.5f);
        Gizmos.DrawWireSphere(doorPivot.position, 0.03f);
    }
}
