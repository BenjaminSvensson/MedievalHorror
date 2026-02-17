using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour, InputSystem_Actions.IPlayerActions
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.08f;
    [SerializeField] private float gamepadLookSensitivity = 180f;
    [SerializeField] private float maxLookPitch = 86f;
    [SerializeField] private bool lockAndHideCursor = true;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3.8f;
    [SerializeField] private float runSpeed = 6.4f;
    [SerializeField] private float crouchSpeed = 2.2f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float maxStepHeight = 0.35f;
    [SerializeField] private float crouchStepHeight = 0.05f;
    [SerializeField] private float airborneStepOffset = 0.02f;
    [SerializeField] private float groundSnapVelocity = -4f;

    [Header("Crouch")]
    [SerializeField] private float crouchHeight = 1.15f;
    [SerializeField] private float crouchTransitionSpeed = 10f;
    [SerializeField] private float crouchCameraDrop = 0.45f;

    [Header("Lean")]
    [SerializeField] private float leanAngle = 10f;
    [SerializeField] private float leanOffset = 0.14f;
    [SerializeField] private float leanSmooth = 12f;

    [Header("Zoom")]
    [SerializeField] private float zoomStep = 3.5f;
    [SerializeField] private float zoomSmooth = 12f;
    [SerializeField] private float minFov = 25f;
    [SerializeField] private float maxFov = 80f;

    [Header("Head Bob")]
    [SerializeField] private float walkBobFrequency = 8.5f;
    [SerializeField] private float walkBobAmplitude = 0.045f;
    [SerializeField] private float runBobFrequency = 12.5f;
    [SerializeField] private float runBobAmplitude = 0.07f;
    [SerializeField] private float crouchBobFrequency = 6f;
    [SerializeField] private float crouchBobAmplitude = 0.028f;
    [SerializeField] private float idleBobFrequency = 1.6f;
    [SerializeField] private float idleBobAmplitude = 0.01f;
    [SerializeField] private float cameraPositionSmooth = 14f;
    [SerializeField] private float stepBobBoost = 2.2f;
    [SerializeField] private float stepBobThreshold = 0.15f;
    [SerializeField] private float stepBobRecovery = 8f;

    [Header("Found Footage Feel")]
    [SerializeField] private float footageNoiseFrequency = 5f;
    [SerializeField] private Vector3 footageNoiseRotation = new Vector3(0.9f, 0.7f, 1.1f);
    [SerializeField] private float strafeTilt = 2.2f;
    [SerializeField] private float forwardTilt = 1.4f;
    [SerializeField] private float lookSway = 0.08f;
    [SerializeField] private float lookSwaySmooth = 10f;
    [SerializeField] private float jumpUpKick = 2.2f;
    [SerializeField] private float inAirPitchReturn = 4.5f;
    [SerializeField] private float landDownKick = 3.2f;
    [SerializeField] private float kickRecovery = 10f;

    [Header("Interaction")]
    [SerializeField] private float interactionRange = 2.2f;
    [SerializeField] private float interactionSphereRadius = 2f;
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField] private float interactionViewAngle = 75f;
    [SerializeField] private float interactionProbeInterval = 0.06f;

    [Header("Hands")]
    [SerializeField] private bool createPlaceholderHandsIfMissing = true;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private float handPoseSmooth = 10f;
    [SerializeField] private float handReachSmooth = 18f;
    [SerializeField] private float handReachHoldTime = 0.22f;
    [SerializeField] private float handTargetDistancePadding = 0.14f;
    [SerializeField] private float handTargetSideOffset = 0.08f;
    [SerializeField] private float handTargetVerticalOffset = 0.06f;
    [SerializeField] private Vector3 leftHandRestPosition = new Vector3(-0.18f, -0.22f, 0.42f);
    [SerializeField] private Vector3 leftHandRestRotation = new Vector3(24f, -28f, 16f);
    [SerializeField] private Vector3 leftHandReadyPosition = new Vector3(-0.1f, -0.08f, 0.28f);
    [SerializeField] private Vector3 leftHandReadyRotation = new Vector3(18f, -16f, 8f);
    [SerializeField] private Vector3 rightHandRestPosition = new Vector3(0.18f, -0.22f, 0.42f);
    [SerializeField] private Vector3 rightHandRestRotation = new Vector3(24f, 28f, -16f);
    [SerializeField] private Vector3 rightHandReadyPosition = new Vector3(0.1f, -0.08f, 0.28f);
    [SerializeField] private Vector3 rightHandReadyRotation = new Vector3(18f, 16f, -8f);
    [SerializeField] private Vector3 placeholderHandScale = new Vector3(0.07f, 0.1f, 0.07f);
    [SerializeField] private Color placeholderHandColor = new Color(0.78f, 0.62f, 0.49f, 1f);

    private enum HandPoseState
    {
        Rest,
        Ready,
        Reaching
    }

    private InputSystem_Actions inputActions;
    private CharacterController controller;
    private float yaw;
    private float pitch;
    private float verticalVelocity;
    private float coyoteTimer;
    private float standingHeight;
    private Vector3 cameraDefaultLocalPosition;
    private float bobTimer;
    private float noiseTimer;
    private float targetFov;
    private float stepBobIntensity;
    private float currentLeanAngle;
    private float currentLeanOffset;
    private float jumpLandPitchOffset;
    private Vector2 smoothedLookDelta;
    private Vector3 smoothedTilt;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 defaultControllerCenter;
    private bool isCrouching;
    private bool wasGrounded;
    private bool sprintHeld;
    private bool crouchHeld;
    private bool jumpPressed;
    private bool leanLeftHeld;
    private bool leanRightHeld;
    private bool lookFromPointer;
    private float lastYPosition;
    private bool wasJumping;
    private float nextInteractionProbeTime;
    private float reachTimer;
    private bool interactPressed;
    private Vector3 currentInteractionPoint;
    private IPlayerInteractable currentInteractable;
    private readonly Collider[] interactionHits = new Collider[24];
    private HandPoseState handPoseState = HandPoseState.Rest;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputActions = new InputSystem_Actions();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (playerCamera == null)
        {
            Debug.LogError($"{nameof(PlayerController)} requires a Camera reference.", this);
            enabled = false;
            return;
        }

        standingHeight = controller.height;
        defaultControllerCenter = controller.center;
        crouchHeight = Mathf.Max(crouchHeight, controller.radius * 2f + 0.05f);
        maxStepHeight = Mathf.Clamp(maxStepHeight, 0f, standingHeight - 0.01f);
        crouchStepHeight = Mathf.Clamp(crouchStepHeight, 0f, crouchHeight - 0.01f);
        controller.stepOffset = maxStepHeight;
        cameraDefaultLocalPosition = playerCamera.transform.localPosition;
        targetFov = maxFov;
        playerCamera.fieldOfView = maxFov;
        lastYPosition = transform.position.y;

        if (lockAndHideCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        EnsureHandsExist();
        SnapHandsToCurrentPose();
    }

    private void OnEnable()
    {
        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
        }

        inputActions.Player.SetCallbacks(this);
        inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        if (inputActions == null)
        {
            return;
        }

        inputActions.Player.SetCallbacks(null);
        inputActions.Player.Disable();
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }

    private void Update()
    {
        ReadInput();
        UpdateInteractionTarget();
        ProcessInteractionInput();
        UpdateLook();
        UpdateMovement();
        UpdateCrouch();
        UpdateZoom();
        UpdateCameraEffects();
        UpdateHands();
    }

    private void ReadInput()
    {
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        isCrouching = crouchHeld || !CanStand();
    }

    private void UpdateLook()
    {
        float lookScale = lookFromPointer ? mouseSensitivity : (gamepadLookSensitivity * Time.deltaTime);
        float lookX = lookInput.x * lookScale;
        float lookY = lookInput.y * lookScale;

        yaw += lookX;
        pitch = Mathf.Clamp(pitch - lookY, -maxLookPitch, maxLookPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        Vector2 targetSway = new Vector2(-lookY, lookX) * lookSway;
        smoothedLookDelta = Vector2.Lerp(smoothedLookDelta, targetSway, Time.deltaTime * lookSwaySmooth);
    }

    private void UpdateMovement()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundSnapVelocity;
        }

        float groundedStepHeight = isCrouching ? crouchStepHeight : maxStepHeight;
        controller.stepOffset = isGrounded ? groundedStepHeight : airborneStepOffset;

        bool running = sprintHeld && !isCrouching && moveInput.y > 0.05f;
        float speed = isCrouching ? crouchSpeed : (running ? runSpeed : walkSpeed);

        Vector3 desiredMove = (transform.right * moveInput.x + transform.forward * moveInput.y) * speed;

        if (coyoteTimer > 0f && jumpPressed && !isCrouching)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpLandPitchOffset += jumpUpKick;
            wasJumping = true;
            coyoteTimer = 0f;
        }
        jumpPressed = false;

        verticalVelocity += gravity * Time.deltaTime;
        desiredMove.y = verticalVelocity;

        controller.Move(desiredMove * Time.deltaTime);

        if (!wasGrounded && controller.isGrounded && verticalVelocity < -8f)
        {
            jumpLandPitchOffset -= landDownKick;
            wasJumping = false;
        }

        wasGrounded = controller.isGrounded;
    }

    private void UpdateCrouch()
    {
        float desiredHeight = isCrouching ? crouchHeight : standingHeight;
        controller.height = Mathf.Lerp(controller.height, desiredHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.center = new Vector3(defaultControllerCenter.x, controller.height * 0.5f, defaultControllerCenter.z);
    }

    private void UpdateZoom()
    {
        float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            float normalizedScroll = Mathf.Abs(scroll) > 20f ? scroll / 120f : scroll;
            targetFov -= normalizedScroll * zoomStep;
            targetFov = Mathf.Clamp(targetFov, minFov, maxFov);
        }

        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, Time.deltaTime * zoomSmooth);
    }

    private void UpdateCameraEffects()
    {
        float currentY = transform.position.y;
        float verticalPositionDelta = (currentY - lastYPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastYPosition = currentY;

        if (controller.isGrounded)
        {
            float stepAmount = Mathf.Abs(verticalPositionDelta);
            if (stepAmount > stepBobThreshold)
            {
                stepBobIntensity = Mathf.Clamp01(stepAmount * 0.25f);
            }
        }

        stepBobIntensity = Mathf.MoveTowards(stepBobIntensity, 0f, Time.deltaTime * stepBobRecovery);

        Vector3 flatVelocity = controller.velocity;
        flatVelocity.y = 0f;
        float speed = flatVelocity.magnitude;
        bool moving = speed > 0.1f;
        bool grounded = controller.isGrounded;

        float bobFrequency;
        float bobAmplitude;

        if (moving && grounded)
        {
            bool running = sprintHeld && !isCrouching && moveInput.y > 0.05f;
            bobFrequency = isCrouching ? crouchBobFrequency : (running ? runBobFrequency : walkBobFrequency);
            bobAmplitude = isCrouching ? crouchBobAmplitude : (running ? runBobAmplitude : walkBobAmplitude);
            bobTimer += Time.deltaTime * bobFrequency * Mathf.Clamp01(speed / runSpeed + 0.2f);
        }
        else
        {
            bobFrequency = idleBobFrequency;
            bobAmplitude = idleBobAmplitude;
            bobTimer += Time.deltaTime * bobFrequency;
        }

        bobAmplitude *= 1f + (stepBobIntensity * stepBobBoost);

        float bobX = Mathf.Cos(bobTimer) * bobAmplitude * 0.5f;
        float bobY = Mathf.Abs(Mathf.Sin(bobTimer * 2f)) * bobAmplitude;
        float bobRoll = Mathf.Sin(bobTimer) * bobAmplitude * 45f;
        float bobPitch = Mathf.Sin(bobTimer * 2f) * bobAmplitude * 20f;

        float stepPitch = -verticalPositionDelta * 0.015f * stepBobIntensity;
        float stepRoll = Mathf.Clamp(verticalPositionDelta * 0.02f, -2.5f, 2.5f) * stepBobIntensity;

        int leanDirection = 0;
        if (leanLeftHeld) leanDirection -= 1;
        if (leanRightHeld) leanDirection += 1;

        float targetLeanAngle = -leanDirection * leanAngle;
        float targetLeanOffset = leanDirection * leanOffset;
        currentLeanAngle = Mathf.Lerp(currentLeanAngle, targetLeanAngle, Time.deltaTime * leanSmooth);
        currentLeanOffset = Mathf.Lerp(currentLeanOffset, targetLeanOffset, Time.deltaTime * leanSmooth);

        Vector3 localVelocity = transform.InverseTransformDirection(flatVelocity);
        Vector3 targetTilt = new Vector3(
            -localVelocity.z * forwardTilt * 0.1f,
            0f,
            -localVelocity.x * strafeTilt * 0.1f
        );
        smoothedTilt = Vector3.Lerp(smoothedTilt, targetTilt, Time.deltaTime * 8f);

        noiseTimer += Time.deltaTime * footageNoiseFrequency;
        float noisePitch = (Mathf.PerlinNoise(noiseTimer, 0.13f) - 0.5f) * 2f * footageNoiseRotation.x;
        float noiseYaw = (Mathf.PerlinNoise(0.17f, noiseTimer) - 0.5f) * 2f * footageNoiseRotation.y;
        float noiseRoll = (Mathf.PerlinNoise(noiseTimer * 0.7f, 0.31f) - 0.5f) * 2f * footageNoiseRotation.z;

        if (!controller.isGrounded && wasJumping)
        {
            jumpLandPitchOffset = Mathf.MoveTowards(jumpLandPitchOffset, 0f, Time.deltaTime * inAirPitchReturn);
        }
        else
        {
            jumpLandPitchOffset = Mathf.Lerp(jumpLandPitchOffset, 0f, Time.deltaTime * kickRecovery);
        }

        float finalPitch = pitch + bobPitch + stepPitch + noisePitch + smoothedLookDelta.x + smoothedTilt.x + jumpLandPitchOffset;
        float finalYaw = noiseYaw + smoothedLookDelta.y;
        float finalRoll = currentLeanAngle + bobRoll + stepRoll + noiseRoll + smoothedTilt.z;

        playerCamera.transform.localRotation = Quaternion.Euler(finalPitch, finalYaw, finalRoll);

        float crouchOffset = isCrouching ? crouchCameraDrop : 0f;
        Vector3 targetLocalPos = new Vector3(
            cameraDefaultLocalPosition.x + currentLeanOffset + bobX,
            cameraDefaultLocalPosition.y - crouchOffset + bobY,
            cameraDefaultLocalPosition.z
        );

        playerCamera.transform.localPosition = Vector3.Lerp(
            playerCamera.transform.localPosition,
            targetLocalPos,
            Time.deltaTime * cameraPositionSmooth
        );
    }

    private void UpdateInteractionTarget()
    {
        if (Time.time < nextInteractionProbeTime)
        {
            return;
        }

        nextInteractionProbeTime = Time.time + interactionProbeInterval;
        currentInteractable = null;
        currentInteractionPoint = Vector3.zero;

        Vector3 origin = playerCamera.transform.position;
        Vector3 viewForward = playerCamera.transform.forward;
        float minDot = Mathf.Cos(interactionViewAngle * Mathf.Deg2Rad);
        float bestScore = float.MinValue;

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            interactionSphereRadius,
            interactionHits,
            interactionMask,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = interactionHits[i];
            if (hit == null)
            {
                continue;
            }

            IPlayerInteractable interactable = hit.GetComponentInParent<IPlayerInteractable>();
            if (interactable == null || !interactable.CanInteract(this) || interactable is not Component interactableComponent)
            {
                continue;
            }

            Vector3 targetPoint = interactable.GetInteractionPoint();
            Vector3 toTarget = targetPoint - origin;
            float distance = toTarget.magnitude;

            if (distance > interactionRange || distance < 0.001f)
            {
                continue;
            }

            Vector3 toTargetDir = toTarget / distance;
            float dot = Vector3.Dot(viewForward, toTargetDir);
            if (dot < minDot)
            {
                continue;
            }

            if (Physics.Raycast(origin, toTargetDir, out RaycastHit sightHit, distance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                bool sameInteractable =
                    sightHit.collider.GetComponentInParent<IPlayerInteractable>() == interactable ||
                    sightHit.collider.transform.IsChildOf(interactableComponent.transform);

                if (!sameInteractable)
                {
                    continue;
                }
            }

            float score = (dot * 2f) - (distance / interactionRange);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            currentInteractable = interactable;
            currentInteractionPoint = targetPoint;
        }

        if (handPoseState != HandPoseState.Reaching)
        {
            handPoseState = currentInteractable != null ? HandPoseState.Ready : HandPoseState.Rest;
        }
    }

    private void ProcessInteractionInput()
    {
        if (!interactPressed)
        {
            return;
        }

        interactPressed = false;

        if (currentInteractable == null)
        {
            return;
        }

        reachTimer = handReachHoldTime;
        handPoseState = HandPoseState.Reaching;
        currentInteractable.Interact(this);
    }

    private void UpdateHands()
    {
        if (reachTimer > 0f)
        {
            reachTimer -= Time.deltaTime;
        }
        else if (handPoseState == HandPoseState.Reaching)
        {
            handPoseState = currentInteractable != null ? HandPoseState.Ready : HandPoseState.Rest;
        }

        UpdateSingleHand(leftHand, true);
        UpdateSingleHand(rightHand, false);
    }

    private void UpdateSingleHand(Transform hand, bool isLeftHand)
    {
        if (hand == null)
        {
            return;
        }

        Vector3 targetLocalPosition;
        Quaternion targetLocalRotation;

        if (handPoseState == HandPoseState.Reaching && currentInteractable != null)
        {
            GetReachPose(hand, isLeftHand, out targetLocalPosition, out targetLocalRotation);
        }
        else
        {
            GetIdlePose(isLeftHand, out targetLocalPosition, out targetLocalRotation);
        }

        float smooth = handPoseState == HandPoseState.Reaching ? handReachSmooth : handPoseSmooth;
        hand.localPosition = Vector3.Lerp(hand.localPosition, targetLocalPosition, Time.deltaTime * smooth);
        hand.localRotation = Quaternion.Slerp(hand.localRotation, targetLocalRotation, Time.deltaTime * smooth);
    }

    private void GetIdlePose(bool isLeftHand, out Vector3 localPosition, out Quaternion localRotation)
    {
        bool isReady = handPoseState == HandPoseState.Ready;

        if (isLeftHand)
        {
            localPosition = isReady ? leftHandReadyPosition : leftHandRestPosition;
            Vector3 euler = isReady ? leftHandReadyRotation : leftHandRestRotation;
            localRotation = Quaternion.Euler(euler);
            return;
        }

        localPosition = isReady ? rightHandReadyPosition : rightHandRestPosition;
        Vector3 rightEuler = isReady ? rightHandReadyRotation : rightHandRestRotation;
        localRotation = Quaternion.Euler(rightEuler);
    }

    private void GetReachPose(Transform hand, bool isLeftHand, out Vector3 localPosition, out Quaternion localRotation)
    {
        Transform parent = hand.parent;
        if (parent == null)
        {
            localPosition = hand.localPosition;
            localRotation = hand.localRotation;
            return;
        }

        Vector3 cameraPosition = playerCamera.transform.position;
        Vector3 toTarget = currentInteractionPoint - cameraPosition;
        float targetDistance = Mathf.Clamp(toTarget.magnitude - handTargetDistancePadding, 0.25f, interactionRange);
        Vector3 targetWorld = cameraPosition + toTarget.normalized * targetDistance;

        float sideDirection = isLeftHand ? -1f : 1f;
        targetWorld += playerCamera.transform.right * (sideDirection * handTargetSideOffset);
        targetWorld -= playerCamera.transform.up * handTargetVerticalOffset;

        localPosition = parent.InverseTransformPoint(targetWorld);

        Vector3 lookDirection = currentInteractionPoint - targetWorld;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            lookDirection = playerCamera.transform.forward;
        }

        Quaternion worldRotation = Quaternion.LookRotation(lookDirection.normalized, playerCamera.transform.up);
        localRotation = Quaternion.Inverse(parent.rotation) * worldRotation;
    }

    private void SnapHandsToCurrentPose()
    {
        if (leftHand != null)
        {
            leftHand.localPosition = leftHandRestPosition;
            leftHand.localRotation = Quaternion.Euler(leftHandRestRotation);
        }

        if (rightHand != null)
        {
            rightHand.localPosition = rightHandRestPosition;
            rightHand.localRotation = Quaternion.Euler(rightHandRestRotation);
        }
    }

    private void EnsureHandsExist()
    {
        if (!createPlaceholderHandsIfMissing)
        {
            return;
        }

        if (leftHand == null)
        {
            leftHand = CreatePlaceholderHand("LeftHand", true);
        }

        if (rightHand == null)
        {
            rightHand = CreatePlaceholderHand("RightHand", false);
        }
    }

    private Transform CreatePlaceholderHand(string handName, bool isLeftHand)
    {
        GameObject handObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        handObject.name = handName;
        handObject.transform.SetParent(playerCamera.transform, false);
        handObject.transform.localScale = placeholderHandScale;
        handObject.layer = gameObject.layer;

        if (handObject.TryGetComponent(out Collider handCollider))
        {
            Destroy(handCollider);
        }

        if (handObject.TryGetComponent(out Renderer handRenderer))
        {
            handRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            handRenderer.receiveShadows = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                Material handMaterial = new Material(shader);
                handMaterial.color = placeholderHandColor;
                handRenderer.material = handMaterial;
            }
        }

        Vector3 localEuler = isLeftHand ? leftHandRestRotation : rightHandRestRotation;
        handObject.transform.localRotation = Quaternion.Euler(localEuler);

        return handObject.transform;
    }

    private bool CanStand()
    {
        if (controller.height >= standingHeight - 0.01f)
        {
            return true;
        }

        float radius = Mathf.Max(0.01f, controller.radius - 0.02f);
        float castDistance = standingHeight - controller.height;
        Vector3 origin = transform.position + Vector3.up * (controller.height - radius);

        return !Physics.SphereCast(origin, radius, Vector3.up, out _, castDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
        lookFromPointer = context.control != null && context.control.device is Pointer;
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            interactPressed = true;
        }
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
        crouchHeld = context.ReadValueAsButton();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            jumpPressed = true;
        }
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        sprintHeld = context.ReadValueAsButton();
    }

    public void OnPrevious(InputAction.CallbackContext context) { }

    public void OnNext(InputAction.CallbackContext context) { }

    public void OnLeanLeft(InputAction.CallbackContext context)
    {
        leanLeftHeld = context.ReadValueAsButton();
    }

    public void OnLeanRight(InputAction.CallbackContext context)
    {
        leanRightHeld = context.ReadValueAsButton();
    }
}
