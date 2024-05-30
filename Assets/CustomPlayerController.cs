using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using TMPro;
using Mirror;


public class CustomPlayerController : NetworkBehaviour
{
    private static Transform localCameraHolder; // "���������" ������ ���������� ������, ������������ ��� �������� ���������� ��� ������� ������� � ������� ������ ���������� ������
    private static readonly string tagMedkit = "Medkit";
    private static readonly int layerPlayer = 6;

    public Transform cameraHolder;
    public float speed, sensitivity, maxForce, jumpForce;
    public Transform statusBarTransform;
    public TMP_Text textHP;
    public TMP_Text textName;

    public bool grounded;   // ���������� ������ (����� �� �� �����������)
    // ��������� ����������, �������
    public float groundDistance;
    public Transform groundCheck;
    public LayerMask groundMask;
    public float fallDamageVelocity;    // ����� ��������, ��� ������� ����� ������ ����
    public float fallDamage;            // ���� ��� �������, ������� �� �������� �������

    private Rigidbody rBody;
    private Animator animator;
    private Vector2 move, look;     // ������ ���� ������ (�����������, ������)
    private float lookRotation;
    private float lastVelocityY;    // �������� ������ �� ��� Y
    private int localHP;            // !!!��������� ���� �������� ������ (HP), ���������� � ������ ������������ � ����������� ��������� ������������ (������� � ����� ����� �� X,Z), ����� ��� ��� �������� HP ����������� ����� ������, �� � ����������� ���������� ������������ OnTriggerEnter ����� ���������� �������� HP

    // ���������������� ���������� (hook ���������� ����� �������, ������� ����� ����������� ��� ���������� �������� � �������)
    [SyncVar(hook = nameof(SyncNickname))]
    public string playerName;
    [SyncVar(hook = nameof(SyncHP))]
    public int HP; // ���� �������� ������
    [SyncVar(hook = nameof(SyncDead))]
    private bool isDead;


    private void Awake()
    {
        rBody = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    public override void OnStartLocalPlayer()   // ���������� ������ ��� ������� ������ �� ��������� ������� (���������� ������) ����� OnStartClient (��� ����� Start, �� ���������� ������)
    {
        cameraHolder.gameObject.AddComponent<Camera>(); // ������� ������
        localCameraHolder = cameraHolder;
        localHP = HP;
        GetComponent<PlayerInput>().enabled = true; // ������� ���� ��������
        GameManager.SetLayerWithChildren(gameObject.transform, layerPlayer);    // ���������� ���� Player
    }

    private void FixedUpdate()
    {
        if (isLocalPlayer && !isDead)
        {
            // �������� ����������, �������/������
            if (Physics.CheckSphere(groundCheck.position, groundDistance, groundMask))
            {
                if (!grounded)
                {
                    grounded = true;
                    animator.SetBool("isFalling", false);

                    float minVelocity = rBody.velocity.y < lastVelocityY ? rBody.velocity.y : lastVelocityY;    // �������� �� ����������� ������ FixedUpdate ����� ���� ������
                    if (minVelocity < fallDamageVelocity)   // �������� ����, ���� �������� ���� ���������� ��������
                    {
                        TakeDamage((int)Mathf.Abs((minVelocity - fallDamageVelocity) * fallDamage));
                    }
                }
            }
            else
            {
                grounded = false;
                animator.SetBool("isFalling", true);
            }

            lastVelocityY = rBody.velocity.y;
        }

        Move();
    }

    private void Update()
    {
        statusBarTransform.LookAt(localCameraHolder);   // ��������� ���������� ��� ������� ���������� (�������, ��) �� ������ ���������� ���������
        
        if (!isLocalPlayer) return;

        Look();
    }

    // ����������� ������
    private void Move()
    {
        if (isDead)
        {
            move = Vector2.zero;
        }

        Vector3 currentVelocity = rBody.velocity;
        Vector3 targetVelocity = new Vector3(move.x, 0, move.y);
        targetVelocity *= speed;                                        // ��������� ������� ��������
        targetVelocity = transform.TransformDirection(targetVelocity);  // ��������� �����������
        Vector3 velocityChange = targetVelocity - currentVelocity;      // ��������� ����
        velocityChange = new Vector3(velocityChange.x, 0, velocityChange.z);
        Vector3.ClampMagnitude(velocityChange, maxForce);               // ���������� ����
        rBody.AddForce(velocityChange, ForceMode.VelocityChange);       // ForceMode.VelocityChange - ��������� �������� ��������
    }

    // ��������� ���������� ������� � �������� ������
    private void Look()
    {
        // ����������� �������
        lookRotation += -look.y * sensitivity;
        lookRotation = Mathf.Clamp(lookRotation, -90, 60);  // ���������� ������ �����/����
        cameraHolder.eulerAngles = new Vector3(lookRotation, cameraHolder.eulerAngles.y, cameraHolder.eulerAngles.z);

        if (isDead) return;

        // ��������� ������ � ������� �������
        transform.Rotate(Vector3.up * look.x * sensitivity);
    }

    // ������
    private void Jump()
    {
        rBody.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
    }

    // ����� ����� ������������
    public void OnMove(InputAction.CallbackContext context)
    {
        move = context.ReadValue<Vector2>();
        animator.SetBool("isRunning", (move.x != 0) || (move.y != 0));
    }

    // ����� ����� ����������� �������
    public void OnLook(InputAction.CallbackContext context)
    {
        look = context.ReadValue<Vector2>();
    }

    // ����� ����� ������
    public void OnJump(InputAction.CallbackContext context)
    {
        if (grounded && !isDead)
        {
            Jump();
        }
    }

    // �����, ������� ����� ����������� �� ������� ��� ���������� �������� �� �������
    public void SyncNickname(string oldValue, string newValue)
    {
        textName.text = playerName;
    }

    [Command]   // ������� ����� �� �������, ����� ��������� ��� �� �������
    public void CmdSetHP(int _HP)   // ���������� ������ ������������ �� ������, ����� ������ ��������� sync vars �� ���� ��������
    {
        HP = _HP;
    }

    // ��������� �� � ������� ������
    [Command]
    public void CmdSetHPAndDestroySource(int _HP, GameObject source)
    {
        NetworkServer.Destroy(source);
        HP = _HP;
    }

    // ��������� �� � ��������� ������
    [Command]
    public void CmdSetHPAndDead(int _HP, bool _isDead)
    {
        HP = _HP;
        isDead = _isDead;
    }

    private void SyncHP(int oldValue, int newValue)
    {
        localHP = newValue;
        textHP.text = HP.ToString();
    }

    // �������� ������� ������ ����� ��������� ���������� ������
    IEnumerator RespawnIn(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        GameManager.ReplacePlayer(connectionToClient);
    }

    private void SyncDead(bool oldValue, bool newValue)
    {
        animator.SetTrigger("isDead");
        StartCoroutine(RespawnIn(3));
    }

    public void TakeDamage(int value)
    {
        if (value < HP)
        {
            CmdSetHP(HP - value);
        }
        else
        {
            CmdSetHPAndDead(0, true);
        } 
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(tagMedkit) && localHP < 100)
        {
            localHP = localHP + 30 >= 100 ? 100 : localHP + 30;
            CmdSetHPAndDestroySource(localHP, other.gameObject);
        }
    }
}
