using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

[RequireComponent(typeof(CharacterController))]
public class PlayerControllerScript : MonoBehaviour
{
    public enum PlayerMap { Player1, Player2 }

    [Header("Configurações de Movimento")]
    public PlayerMap playerMap = PlayerMap.Player1;
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask;

    // -------------------------------------------------------------------------
    // Privados
    // -------------------------------------------------------------------------
    private CharacterController controller;
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isGrounded;

    private Players_ActionMaps inputActions;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputActionMap activePlayerMap;

    // InputUser faz o pareamento exclusivo dispositivo <-> actions
    private InputUser inputUser;
    private bool userCreated = false;

    // -------------------------------------------------------------------------
    // Inicialização
    // -------------------------------------------------------------------------
    private void Awake()
    {
        controller  = GetComponent<CharacterController>();
        inputActions = new Players_ActionMaps();
        SetupPlayerActions();
    }

    private void SetupPlayerActions()
    {
        if (playerMap == PlayerMap.Player1)
        {
            moveAction      = inputActions.Player1.Move;
            jumpAction      = inputActions.Player1.Jump;
            activePlayerMap = inputActions.Player1;
        }
        else
        {
            moveAction      = inputActions.Player2.Move;
            jumpAction      = inputActions.Player2.Jump;
            activePlayerMap = inputActions.Player2;
        }
    }

    // -------------------------------------------------------------------------
    // API pública — chamada pelo GamepadAssigner
    // -------------------------------------------------------------------------

    /// <summary>
    /// Associa um ou mais dispositivos a este player via InputUser.
    /// Pode ser chamado com um Gamepad, Keyboard, Mouse, etc.
    /// </summary>
    public void BindDevices(params InputDevice[] devices)
    {
        EnsureUserCreated();

        // Remove todos os dispositivos anteriores
        inputUser.UnpairDevices();

        foreach (var device in devices)
        {
            if (device != null)
                InputUser.PerformPairingWithDevice(device, user: inputUser);
        }

        // Reativa o action map após o repareamento
        activePlayerMap.Disable();
        activePlayerMap.Enable();

        string names = string.Join(" + ", System.Array.ConvertAll(devices, d => d?.displayName ?? "null"));
        Debug.Log($"[{gameObject.name}] Dispositivos pareados: {names}");
    }

    /// <summary>
    /// Remove pareamentos, liberando todos os dispositivos deste player.
    /// </summary>
    public void UnbindDevices()
    {
        if (!userCreated) return;
        inputUser.UnpairDevices();

        activePlayerMap.Disable();
        activePlayerMap.Enable();

        Debug.Log($"[{gameObject.name}] Dispositivos desvinculados.");
    }

    private void EnsureUserCreated()
    {
        if (userCreated) return;
        inputUser   = InputUser.CreateUserWithoutPairedDevices();
        inputUser.AssociateActionsWithUser(inputActions);
        userCreated = true;
    }

    // -------------------------------------------------------------------------
    // Enable / Disable
    // -------------------------------------------------------------------------
    private void OnEnable()
    {
        moveAction.performed += OnMovePerformed;
        moveAction.canceled  += OnMoveCanceled;
        jumpAction.performed += OnJumpPerformed;
        activePlayerMap.Enable();
    }

    private void OnDisable()
    {
        moveAction.performed -= OnMovePerformed;
        moveAction.canceled  -= OnMoveCanceled;
        jumpAction.performed -= OnJumpPerformed;
        activePlayerMap.Disable();
    }

    private void OnDestroy()
    {
        if (userCreated)
        {
            inputUser.UnpairDevicesAndRemoveUser();
            userCreated = false;
        }
        inputActions?.Dispose();
    }

    // -------------------------------------------------------------------------
    // Callbacks de Input
    // -------------------------------------------------------------------------
    private void OnMovePerformed(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx)  => moveInput  = Vector2.zero;

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    // -------------------------------------------------------------------------
    // Update
    // -------------------------------------------------------------------------
    private void Update()
    {
        // Checa chão
        if (groundCheck != null)
            isGrounded = Physics.Raycast(groundCheck.position, Vector3.down, out _, groundDistance, groundMask);
        else
            isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        // Movimento horizontal
        Vector3 move = new Vector3(moveInput.x, 0f, moveInput.y);

        if (move.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move.normalized);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Gravidade
        velocity.y += gravity * Time.deltaTime;

        Vector3 displacement = (move * moveSpeed + velocity) * Time.deltaTime;
        controller.Move(displacement);
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(groundCheck.position, Vector3.down * groundDistance);
    }
}
