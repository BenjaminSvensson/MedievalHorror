using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private Transform leftHandRaisedPose;
    [SerializeField] private Transform rightHandRaisedPose;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private bool ignoreOwnColliders = true;
    [SerializeField] private bool debugDrawRaycast = true;
    [SerializeField] private bool debugDrawGizmos = true;
    [SerializeField] private Color debugNoHitColor = Color.red;
    [SerializeField] private Color debugHitColor = Color.green;

    [Header("Hand Pose")]
    [SerializeField] private bool hideHandsWithoutTarget = true;
    [SerializeField] private bool useCameraRelativeFallbackPose = true;
    [SerializeField] private Transform leftHandHiddenPose;
    [SerializeField] private Transform rightHandHiddenPose;
    [SerializeField] private float hiddenForward = 0.12f;
    [SerializeField] private float hiddenSide = 0.46f;
    [SerializeField] private float hiddenVertical = -0.42f;
    [SerializeField] private float hiddenViewportMargin = 0.05f;
    [SerializeField] private bool debugSnapHandsToTarget = false;
    [SerializeField] private float debugHandSurfaceOffset = 0.03f;
    [SerializeField] private float debugHandsSeparation = 0.09f;
    [SerializeField] private float fallbackForward = 0.38f;
    [SerializeField] private float fallbackSide = 0.16f;
    [SerializeField] private float fallbackVertical = -0.12f;
    [SerializeField] private Vector3 leftHandInteractOffset = new Vector3(0f, 0f, 0.18f);
    [SerializeField] private Vector3 rightHandInteractOffset = new Vector3(0f, 0f, 0.18f);
    [SerializeField] private Vector3 leftHandInteractRotation = new Vector3(-10f, -8f, 6f);
    [SerializeField] private Vector3 rightHandInteractRotation = new Vector3(-10f, 8f, -6f);
    [SerializeField] private float handPoseLerpSpeed = 10f;
    [SerializeField] private float handFollowCatchupSpeed = 28f;

    private IInteractable currentInteractable;

    private Vector3 leftHandDefaultPos;
    private Vector3 rightHandDefaultPos;
    private Quaternion leftHandDefaultRot;
    private Quaternion rightHandDefaultRot;
    private bool leftCaptured;
    private bool rightCaptured;
    private readonly RaycastHit[] raycastHits = new RaycastHit[16];
    private Renderer[] leftHandRenderers;
    private Renderer[] rightHandRenderers;
    private Vector3 currentTargetPoint;
    private Vector3 currentTargetNormal;
    private Transform currentTargetTransform;
    private bool rightHandOverrideActive;
    private Vector3 rightHandOverrideWorldPos;
    private Quaternion rightHandOverrideWorldRot;
    private float rightHandOverrideTimeRemaining;
    private bool interactHeld;
    private bool rightHandFollowActive;
    private Transform rightHandFollowTarget;
    private Vector3 rightHandFollowLocalOffset;
    private Quaternion rightHandFollowLocalRotationOffset = Quaternion.identity;

    public bool HasTarget => currentInteractable != null;
    public IInteractable CurrentInteractable => currentInteractable;
    public bool IsInteractHeld => interactHeld;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (rayOrigin == null && playerCamera != null)
        {
            rayOrigin = playerCamera.transform;
        }

        CaptureDefaultHandPose();
        CacheHandRenderers();
        UpdateHandVisibility();
    }

    private void Update()
    {
        UpdateCurrentInteractable();
    }

    private void LateUpdate()
    {
        if (rightHandOverrideActive)
        {
            rightHandOverrideTimeRemaining -= Time.deltaTime;
            if (rightHandOverrideTimeRemaining <= 0f)
            {
                rightHandOverrideActive = false;
            }
        }

        UpdateHandPose();
        UpdateHandVisibility();
    }

    public void TryInteract()
    {
        if (currentInteractable == null)
        {
            return;
        }

        currentInteractable.Interact(this);
    }

    public void SetInteractHeld(bool held)
    {
        interactHeld = held;
        if (!interactHeld)
        {
            StopRightHandFollowTarget();
        }
    }

    public bool TryGetCurrentTargetHit(out Vector3 point, out Vector3 normal)
    {
        if (currentTargetTransform != null)
        {
            point = currentTargetPoint;
            normal = currentTargetNormal.sqrMagnitude > 0.0001f ? currentTargetNormal.normalized : transform.forward;
            return true;
        }

        point = default;
        normal = default;
        return false;
    }

    public void SetRightHandWorldOverride(Vector3 worldPosition, Quaternion worldRotation, float holdDuration)
    {
        rightHandOverrideActive = true;
        rightHandOverrideWorldPos = worldPosition;
        rightHandOverrideWorldRot = worldRotation;
        rightHandOverrideTimeRemaining = Mathf.Max(0f, holdDuration);
    }

    public void SetRightHandFollowTarget(Transform target, Vector3 localOffset, Vector3 localEulerOffset)
    {
        if (target == null)
        {
            return;
        }

        rightHandFollowActive = true;
        rightHandFollowTarget = target;
        rightHandFollowLocalOffset = localOffset;
        rightHandFollowLocalRotationOffset = Quaternion.Euler(localEulerOffset);
    }

    public void StopRightHandFollowTarget()
    {
        rightHandFollowActive = false;
        rightHandFollowTarget = null;
    }

    public void ForceRightHandWorldPose(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (rightHand == null || !rightCaptured || rightHand.parent == null)
        {
            return;
        }

        rightHand.localPosition = rightHand.parent.InverseTransformPoint(worldPosition);
        rightHand.localRotation = Quaternion.Inverse(rightHand.parent.rotation) * worldRotation;
    }

    private void UpdateCurrentInteractable()
    {
        if (playerCamera == null || rayOrigin == null)
        {
            SetCurrentInteractable(null);
            return;
        }

        Ray ray = new Ray(rayOrigin.position, playerCamera.transform.forward);
        int hitCount = Physics.RaycastNonAlloc(ray, raycastHits, interactDistance, interactMask, triggerInteraction);
        if (hitCount > 1)
        {
            System.Array.Sort(raycastHits, 0, hitCount, RaycastHitDistanceComparer.Instance);
        }

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            if (hit.collider == null)
            {
                continue;
            }

            if (ignoreOwnColliders && hit.collider.transform.root == transform.root)
            {
                continue;
            }

            IInteractable hitInteractable = ResolveInteractable(hit.collider);
            if (hitInteractable != null && hitInteractable.CanInteract(this))
            {
                currentTargetPoint = hit.point;
                currentTargetNormal = hit.normal;
                currentTargetTransform = hit.collider.transform;
                DrawDebugRay(ray, hit.distance, true);
                SetCurrentInteractable(hitInteractable);
                return;
            }

            if (!hit.collider.isTrigger)
            {
                break;
            }
        }

        DrawDebugRay(ray, interactDistance, false);
        currentTargetTransform = null;
        SetCurrentInteractable(null);
    }

    private IInteractable ResolveInteractable(Collider hitCollider)
    {
        InteractableTarget explicitTarget = hitCollider.GetComponent<InteractableTarget>();
        if (explicitTarget != null && explicitTarget.TryGetInteractable(out IInteractable linkedInteractable))
        {
            return linkedInteractable;
        }

        return hitCollider.GetComponentInParent<IInteractable>();
    }

    private void SetCurrentInteractable(IInteractable nextInteractable)
    {
        if (ReferenceEquals(currentInteractable, nextInteractable))
        {
            return;
        }

        currentInteractable?.OnFocusExit(this);
        currentInteractable = nextInteractable;
        currentInteractable?.OnFocusEnter(this);
    }

    private void CaptureDefaultHandPose()
    {
        if (leftHand != null)
        {
            leftHandDefaultPos = leftHand.localPosition;
            leftHandDefaultRot = leftHand.localRotation;
            leftCaptured = true;
        }

        if (rightHand != null)
        {
            rightHandDefaultPos = rightHand.localPosition;
            rightHandDefaultRot = rightHand.localRotation;
            rightCaptured = true;
        }

        if (!leftCaptured && !rightCaptured)
        {
            Debug.LogWarning($"{nameof(PlayerInteractor)} has no hand transforms assigned.", this);
        }
    }

    private void CacheHandRenderers()
    {
        leftHandRenderers = leftHand != null ? leftHand.GetComponentsInChildren<Renderer>(true) : null;
        rightHandRenderers = rightHand != null ? rightHand.GetComponentsInChildren<Renderer>(true) : null;
    }

    private void UpdateHandVisibility()
    {
        if (!hideHandsWithoutTarget)
        {
            SetRenderersEnabled(leftHandRenderers, true);
            SetRenderersEnabled(rightHandRenderers, true);
            return;
        }

        bool shouldShowHands = currentInteractable != null || !AreHandsFullyOffScreen();
        SetRenderersEnabled(leftHandRenderers, shouldShowHands);
        SetRenderersEnabled(rightHandRenderers, shouldShowHands);
    }

    private static void SetRenderersEnabled(Renderer[] renderers, bool enabledState)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = enabledState;
            }
        }
    }

    private void UpdateHandPose()
    {
        bool shouldRaiseHands = currentInteractable != null;

        if (leftHand != null && leftCaptured)
        {
            GetTargetHandPose(
                leftHand,
                leftHandRaisedPose,
                true,
                shouldRaiseHands,
                leftHandDefaultPos,
                leftHandDefaultRot,
                leftHandInteractOffset,
                leftHandInteractRotation,
                out Vector3 targetPos,
                out Quaternion targetRot
            );

            leftHand.localPosition = Vector3.Lerp(leftHand.localPosition, targetPos, Time.deltaTime * handPoseLerpSpeed);
            leftHand.localRotation = Quaternion.Slerp(leftHand.localRotation, targetRot, Time.deltaTime * handPoseLerpSpeed);
        }

        if (rightHand != null && rightCaptured)
        {
            float rightHandLerpSpeed = handPoseLerpSpeed;

            GetTargetHandPose(
                rightHand,
                rightHandRaisedPose,
                false,
                shouldRaiseHands,
                rightHandDefaultPos,
                rightHandDefaultRot,
                rightHandInteractOffset,
                rightHandInteractRotation,
                out Vector3 targetPos,
                out Quaternion targetRot
            );

            if (rightHandFollowActive && rightHandFollowTarget != null && rightHand.parent != null)
            {
                Vector3 worldPos = rightHandFollowTarget.TransformPoint(rightHandFollowLocalOffset);
                Quaternion worldRot = rightHandFollowTarget.rotation * rightHandFollowLocalRotationOffset;
                targetPos = rightHand.parent.InverseTransformPoint(worldPos);
                targetRot = Quaternion.Inverse(rightHand.parent.rotation) * worldRot;
                rightHandLerpSpeed = handFollowCatchupSpeed;
            }
            else if (rightHandOverrideActive && rightHand.parent != null)
            {
                targetPos = rightHand.parent.InverseTransformPoint(rightHandOverrideWorldPos);
                targetRot = Quaternion.Inverse(rightHand.parent.rotation) * rightHandOverrideWorldRot;
                rightHandLerpSpeed = handFollowCatchupSpeed;
            }

            rightHand.localPosition = Vector3.Lerp(rightHand.localPosition, targetPos, Time.deltaTime * rightHandLerpSpeed);
            rightHand.localRotation = Quaternion.Slerp(rightHand.localRotation, targetRot, Time.deltaTime * rightHandLerpSpeed);
        }
    }

    private void GetTargetHandPose(
        Transform hand,
        Transform raisedPose,
        bool isLeft,
        bool shouldRaiseHands,
        Vector3 defaultLocalPos,
        Quaternion defaultLocalRot,
        Vector3 interactOffset,
        Vector3 interactRotation,
        out Vector3 targetPos,
        out Quaternion targetRot)
    {
        if (!shouldRaiseHands)
        {
            if (hideHandsWithoutTarget && hand.parent != null)
            {
                Transform hiddenPose = isLeft ? leftHandHiddenPose : rightHandHiddenPose;
                if (hiddenPose != null)
                {
                    targetPos = hand.parent.InverseTransformPoint(hiddenPose.position);
                    targetRot = Quaternion.Inverse(hand.parent.rotation) * hiddenPose.rotation;
                    return;
                }

                if (playerCamera != null)
                {
                    float sideSign = isLeft ? -1f : 1f;
                    Vector3 worldPos = playerCamera.transform.position
                        + playerCamera.transform.forward * hiddenForward
                        + playerCamera.transform.right * (hiddenSide * sideSign)
                        + playerCamera.transform.up * hiddenVertical;
                    Quaternion worldRot = Quaternion.LookRotation(playerCamera.transform.forward, Vector3.up);
                    targetPos = hand.parent.InverseTransformPoint(worldPos);
                    targetRot = Quaternion.Inverse(hand.parent.rotation) * worldRot;
                    return;
                }
            }

            targetPos = defaultLocalPos;
            targetRot = defaultLocalRot;
            return;
        }

        if (raisedPose != null && hand.parent != null)
        {
            targetPos = hand.parent.InverseTransformPoint(raisedPose.position);
            targetRot = Quaternion.Inverse(hand.parent.rotation) * raisedPose.rotation;
            return;
        }

        if (debugSnapHandsToTarget && currentTargetTransform != null && hand.parent != null)
        {
            float sideSign = isLeft ? -1f : 1f;
            Vector3 worldRight = playerCamera != null ? playerCamera.transform.right : transform.right;
            Vector3 worldNormal = currentTargetNormal.sqrMagnitude > 0.0001f ? currentTargetNormal.normalized : transform.forward;
            Vector3 worldPos = currentTargetPoint + worldNormal * debugHandSurfaceOffset + worldRight * (debugHandsSeparation * sideSign);
            Quaternion worldRot = Quaternion.LookRotation(-worldNormal, Vector3.up);

            targetPos = hand.parent.InverseTransformPoint(worldPos);
            targetRot = Quaternion.Inverse(hand.parent.rotation) * worldRot;
            return;
        }

        if (useCameraRelativeFallbackPose && playerCamera != null && hand.parent != null)
        {
            float sideSign = isLeft ? -1f : 1f;
            Vector3 worldPos = playerCamera.transform.position
                + playerCamera.transform.forward * fallbackForward
                + playerCamera.transform.right * (fallbackSide * sideSign)
                + playerCamera.transform.up * fallbackVertical;

            Quaternion worldRot = Quaternion.LookRotation(playerCamera.transform.forward, Vector3.up);
            targetPos = hand.parent.InverseTransformPoint(worldPos);
            targetRot = Quaternion.Inverse(hand.parent.rotation) * worldRot;
            return;
        }

        targetPos = defaultLocalPos + interactOffset;
        targetRot = defaultLocalRot * Quaternion.Euler(interactRotation);
    }

    private bool AreHandsFullyOffScreen()
    {
        if (playerCamera == null)
        {
            return true;
        }

        bool hasAnyHand = false;

        if (leftCaptured && leftHand != null)
        {
            hasAnyHand = true;
            if (!IsOffScreen(leftHand.position))
            {
                return false;
            }
        }

        if (rightCaptured && rightHand != null)
        {
            hasAnyHand = true;
            if (!IsOffScreen(rightHand.position))
            {
                return false;
            }
        }

        return hasAnyHand;
    }

    private bool IsOffScreen(Vector3 worldPosition)
    {
        Vector3 viewport = playerCamera.WorldToViewportPoint(worldPosition);
        if (viewport.z <= 0f)
        {
            return true;
        }

        return viewport.x < -hiddenViewportMargin
            || viewport.x > 1f + hiddenViewportMargin
            || viewport.y < -hiddenViewportMargin
            || viewport.y > 1f + hiddenViewportMargin;
    }

    private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

        public int Compare(RaycastHit a, RaycastHit b)
        {
            return a.distance.CompareTo(b.distance);
        }
    }

    private void DrawDebugRay(Ray ray, float distance, bool hitInteractable)
    {
        if (!debugDrawRaycast)
        {
            return;
        }

        Color color = hitInteractable ? debugHitColor : debugNoHitColor;
        Debug.DrawRay(ray.origin, ray.direction * distance, color, 0f, false);
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDrawGizmos)
        {
            return;
        }

        Transform origin = rayOrigin;
        Camera cam = playerCamera;

        if (origin == null)
        {
            origin = transform;
        }

        if (cam == null)
        {
            cam = GetComponentInChildren<Camera>();
        }

        Vector3 direction = cam != null ? cam.transform.forward : origin.forward;
        Gizmos.color = currentInteractable != null ? debugHitColor : debugNoHitColor;
        Gizmos.DrawLine(origin.position, origin.position + direction * interactDistance);
        Gizmos.DrawWireSphere(origin.position + direction * interactDistance, 0.03f);
    }
}
