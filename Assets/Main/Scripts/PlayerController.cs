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
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float groundedStepOffset = 0.35f;
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
    [SerializeField] private float jumpKick = 2f;
    [SerializeField] private float landKick = 2.5f;
    [SerializeField] private float kickRecovery = 8f;

    private InputSystem_Actions inputActions;
    private CharacterController controller;
    private float yaw;
    private float pitch;
    private float verticalVelocity;
    private float standingHeight;
    private Vector3 cameraDefaultLocalPosition;
    private float bobTimer;
    private float noiseTimer;
    private float targetFov;
    private float stepBobIntensity;
    private float currentLeanAngle;
    private float currentLeanOffset;
    private float currentKick;
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
        cameraDefaultLocalPosition = playerCamera.transform.localPosition;
        targetFov = playerCamera.fieldOfView;
        lastYPosition = transform.position.y;

        if (lockAndHideCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
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
        UpdateLook();
        UpdateMovement();
        UpdateCrouch();
        UpdateZoom();
        UpdateCameraEffects();
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

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundSnapVelocity;
        }

        controller.stepOffset = isGrounded ? groundedStepOffset : airborneStepOffset;

        bool running = sprintHeld && !isCrouching && moveInput.y > 0.05f;
        float speed = isCrouching ? crouchSpeed : (running ? runSpeed : walkSpeed);

        Vector3 desiredMove = (transform.right * moveInput.x + transform.forward * moveInput.y) * speed;

        if (isGrounded && jumpPressed && !isCrouching)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            currentKick += jumpKick;
        }
        jumpPressed = false;

        verticalVelocity += gravity * Time.deltaTime;
        desiredMove.y = verticalVelocity;

        controller.Move(desiredMove * Time.deltaTime);

        if (!wasGrounded && controller.isGrounded && verticalVelocity < -8f)
        {
            currentKick -= landKick;
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

        currentKick = Mathf.Lerp(currentKick, 0f, Time.deltaTime * kickRecovery);

        float finalPitch = pitch + bobPitch + stepPitch + noisePitch + smoothedLookDelta.x + smoothedTilt.x + currentKick;
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

    public void OnInteract(InputAction.CallbackContext context) { }

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
