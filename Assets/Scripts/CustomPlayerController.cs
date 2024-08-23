using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Mirror;


public class CustomPlayerController : NetworkBehaviour
{
    private const string tagMedkit = "Medkit";
    private const int layerPlayer = 6;

    private static Transform localCameraHolder; // "Держатель" камеры локального игрока, используется для поворота информации над головой игроков в сторону камеры локального игрока

    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float speed;
    [SerializeField] private float lookSensitivity;
    [SerializeField] private float lookUpConstraint;
    [SerializeField] private float lookDownConstraint;
    [SerializeField] private float maxForce;
    [SerializeField] private float jumpForce;
    [SerializeField] private int respawnTime;
    [SerializeField] private int medkitHealth;
    [SerializeField] private Transform statusBar;
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text healthText;

    // Настройки заземления, падения
    [SerializeField] private float groundDistance;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float fallDamageVelocity;  // Порог скорости падения, при которой будет нанесён урон
    [SerializeField] private float fallDamage;

    private Rigidbody rigidbodyCached;
    private Animator animator;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float lookRotation;
    private float lastVelocityY;    // Скорость игрока по оси Y
    private int localHealth;        // Локальное здоровье игрока, необходимо в случае столкновения с несколькими аптечками одновременно (аптечки в одной точке по X,Z), иначе так как основное здоровье обновляется через сервер, то в последующих обработках столкновений OnTriggerEnter будет устаревшее значение здоровья
    private int maxHealth;
    private bool isGrounded;        // Стоит ли игрок на поверхности (заземление)

    // Синхронизируемые переменные (hook определяет метод клиента, который будет выполняться при обновлении занчений с сервера)
    [SyncVar(hook = nameof(SyncNickname))]
    public string nickname;
    [SyncVar(hook = nameof(SyncHealth))]
    public int health;
    [SyncVar(hook = nameof(SyncIsDead))]
    private bool isDead;


    private void Awake()
    {
        rigidbodyCached = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        maxHealth = health;
    }

    public override void OnStartLocalPlayer()   // Вызывается ТОЛЬКО для объекта игрока на локальном клиенте (локального игрока) после OnStartClient (как метод Start, но вызывается раньше)
    {
        cameraHolder.gameObject.AddComponent<Camera>(); // Создать камеру
        localCameraHolder = cameraHolder;
        localHealth = health;
        GetComponent<PlayerInput>().enabled = true;     // Сделать ввод активным
        GameManager.SetLayerWithChildren(gameObject.transform, layerPlayer);    // Установить слой Player
    }

    private void FixedUpdate()
    {
        if (isLocalPlayer && !isDead)
        {
            // Проверка заземления, падения/прыжка
            if (Physics.CheckSphere(groundCheck.position, groundDistance, groundMask))
            {
                if (!isGrounded)
                {
                    isGrounded = true;
                    animator.SetBool("isFalling", false);

                    float minVelocity = rigidbodyCached.velocity.y < lastVelocityY ? rigidbodyCached.velocity.y : lastVelocityY;    // Скорость из предыдущего вызова FixedUpdate может быть меньше
                    if (minVelocity < fallDamageVelocity)   // Получить урон, если скорость ниже порогового значения
                    {
                        TakeDamage((int)Mathf.Abs((minVelocity - fallDamageVelocity) * fallDamage));
                    }
                }
            }
            else
            {
                isGrounded = false;
                animator.SetBool("isFalling", true);
            }

            lastVelocityY = rigidbodyCached.velocity.y;
        }

        Move();
    }

    private void Update()
    {
        statusBar.LookAt(localCameraHolder);    // Повернуть информацию над головой персонажей (никнейм, здоровье) на камеру локального персонажа
        
        if (!isLocalPlayer) return;

        Look();
    }

    /// <summary>Перемещение игрока</summary>
    private void Move()
    {
        if (isDead)
        {
            moveInput = Vector2.zero;
        }

        Vector3 currentVelocity = rigidbodyCached.velocity;
        Vector3 targetVelocity = new Vector3(moveInput.x, 0, moveInput.y);
        targetVelocity *= speed;                                            // Посчитать целевую скорость
        targetVelocity = transform.TransformDirection(targetVelocity);      // Выровнять направление
        Vector3 velocityChange = targetVelocity - currentVelocity;          // Посчитать силу
        velocityChange = new Vector3(velocityChange.x, 0, velocityChange.z);
        Vector3.ClampMagnitude(velocityChange, maxForce);                   // Ограничить силу
        rigidbodyCached.AddForce(velocityChange, ForceMode.VelocityChange); // ForceMode.VelocityChange - мгновенно изменить скорость
    }

    /// <summary>Изменение напрвления взгляда и поворота игрока</summary>
    private void Look()
    {
        // Направление взгляда
        lookRotation += -lookInput.y * lookSensitivity;
        lookRotation = Mathf.Clamp(lookRotation, lookUpConstraint, lookDownConstraint); // Ограничить взгляд вверх/вниз
        cameraHolder.eulerAngles = new Vector3(lookRotation, cameraHolder.eulerAngles.y, cameraHolder.eulerAngles.z);

        if (isDead) return;

        // Повернуть игрока в сторону взгляда
        transform.Rotate(lookInput.x * lookSensitivity * Vector3.up);
    }

    private void Jump()
    {
        rigidbodyCached.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
    }

    /// <summary>Обрабатывает ввод передвижения</summary>
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
        animator.SetBool("isRunning", (moveInput.x != 0) || (moveInput.y != 0));
    }

    /// <summary>Обрабатывает ввод направления взгляда</summary>
    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    /// <summary>Обрабатывает ввод прыжка</summary>
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started && isGrounded && !isDead)
        {
            Jump();
        }
    }

    public void OnExit(InputAction.CallbackContext context)
    {
        GameManager.StopGame();
    }

    /// <summary>Вызывается на клиенте при обновлении значения на сервере</summary>
    public void SyncNickname(string oldValue, string newValue)
    {
        nicknameText.text = nickname;
    }

    [Command]   // [Command] - вызывает метод на клиенте, чтобы выполнить код на сервере
    public void CmdSetHealth(int value) // Информация игрока направляется на сервер, затем сервер обновляет sync vars на всех клиентах
    {
        health = value;
    }

    /// <summary>Обновляет здоровье и удаляет объект (аптечку)</summary>
    [Command]
    public void CmdSetHealthAndDestroySource(int newHealth, GameObject source)
    {
        NetworkServer.Destroy(source);
        health = newHealth;
    }

    /// <summary>Обновляет здоровье и состояние смерти</summary>
    [Command]
    public void CmdSetHPAndDead(int newHealth, bool newIsDead)
    {
        health = newHealth;
        isDead = newIsDead;
    }

    private void SyncHealth(int oldValue, int newValue)
    {
        localHealth = newValue;
        healthText.text = health.ToString();
    }

    /// <summary>Вызывает респавн игрока</summary>
    private async UniTaskVoid RespawnInAsync(int millisecondsDelay, CancellationToken cancellationToken)
    {
        await UniTask.Delay(millisecondsDelay, cancellationToken: cancellationToken);
        GameManager.ReplacePlayer(connectionToClient);  // Создать новый объект игрока
    }

    private void SyncIsDead(bool oldValue, bool newValue)
    {
        animator.SetTrigger("isDead");
        RespawnInAsync(respawnTime, this.GetCancellationTokenOnDestroy()).Forget();
    }

    public void TakeDamage(int value)
    {
        if (value < health)
        {
            CmdSetHealth(health - value);
        }
        else
        {
            CmdSetHPAndDead(0, true);
        } 
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(tagMedkit) && localHealth < maxHealth)
        {
            localHealth = localHealth + medkitHealth >= maxHealth ? maxHealth : localHealth + medkitHealth;
            CmdSetHealthAndDestroySource(localHealth, other.gameObject);
        }
    }
}
