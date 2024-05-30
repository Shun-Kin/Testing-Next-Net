using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using TMPro;
using Mirror;


public class CustomPlayerController : NetworkBehaviour
{
    private static Transform localCameraHolder; // "Держатель" камеры локального игрока, используется для поворота информации над головой игроков в сторону камеры локального игрока
    private static readonly string tagMedkit = "Medkit";
    private static readonly int layerPlayer = 6;

    public Transform cameraHolder;
    public float speed, sensitivity, maxForce, jumpForce;
    public Transform statusBarTransform;
    public TMP_Text textHP;
    public TMP_Text textName;

    public bool grounded;   // Заземление игрока (стоит ли на поверхности)
    // Настройки заземления, падения
    public float groundDistance;
    public Transform groundCheck;
    public LayerMask groundMask;
    public float fallDamageVelocity;    // Порог скорости, при которой будет нанесён урон
    public float fallDamage;            // Урон при падении, зависит от скорости падения

    private Rigidbody rBody;
    private Animator animator;
    private Vector2 move, look;     // Хранят ввод игрока (перемещение, взгляд)
    private float lookRotation;
    private float lastVelocityY;    // Скорость игрока по оси Y
    private int localHP;            // !!!Локальные очки здоровья игрока (HP), необходимо в случае столкновения с несколькими аптечками одновременно (аптечки в одной точке по X,Z), иначе так как основное HP обновляется через сервер, то в последующих обработках столкновений OnTriggerEnter будет устаревшее значение HP

    // Синхронизируемые переменные (hook определяет метод клиента, который будет выполняться при обновлении занчений с сервера)
    [SyncVar(hook = nameof(SyncNickname))]
    public string playerName;
    [SyncVar(hook = nameof(SyncHP))]
    public int HP; // Очки здоровья игрока
    [SyncVar(hook = nameof(SyncDead))]
    private bool isDead;


    private void Awake()
    {
        rBody = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    public override void OnStartLocalPlayer()   // Вызывается ТОЛЬКО для объекта игрока на локальном клиенте (локального игрока) после OnStartClient (как метод Start, но вызывается раньше)
    {
        cameraHolder.gameObject.AddComponent<Camera>(); // Создать камеру
        localCameraHolder = cameraHolder;
        localHP = HP;
        GetComponent<PlayerInput>().enabled = true; // Сделать ввод активным
        GameManager.SetLayerWithChildren(gameObject.transform, layerPlayer);    // Установить слой Player
    }

    private void FixedUpdate()
    {
        if (isLocalPlayer && !isDead)
        {
            // Проверка заземления, падения/прыжка
            if (Physics.CheckSphere(groundCheck.position, groundDistance, groundMask))
            {
                if (!grounded)
                {
                    grounded = true;
                    animator.SetBool("isFalling", false);

                    float minVelocity = rBody.velocity.y < lastVelocityY ? rBody.velocity.y : lastVelocityY;    // Скорость из предыдущего вызова FixedUpdate может быть меньше
                    if (minVelocity < fallDamageVelocity)   // Получить урон, если скорость ниже порогового значения
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
        statusBarTransform.LookAt(localCameraHolder);   // Повернуть информацию над головой персонажей (никнейм, хп) на камеру локального персонажа
        
        if (!isLocalPlayer) return;

        Look();
    }

    // Перемещение игрока
    private void Move()
    {
        if (isDead)
        {
            move = Vector2.zero;
        }

        Vector3 currentVelocity = rBody.velocity;
        Vector3 targetVelocity = new Vector3(move.x, 0, move.y);
        targetVelocity *= speed;                                        // Посчитать целевую скорость
        targetVelocity = transform.TransformDirection(targetVelocity);  // Выровнять направление
        Vector3 velocityChange = targetVelocity - currentVelocity;      // Посчитать силу
        velocityChange = new Vector3(velocityChange.x, 0, velocityChange.z);
        Vector3.ClampMagnitude(velocityChange, maxForce);               // Ограничить силу
        rBody.AddForce(velocityChange, ForceMode.VelocityChange);       // ForceMode.VelocityChange - мгновенно изменить скорость
    }

    // Изменение напрвления взгляда и поворота игрока
    private void Look()
    {
        // Направление взгляда
        lookRotation += -look.y * sensitivity;
        lookRotation = Mathf.Clamp(lookRotation, -90, 60);  // Ограничить взгляд вверх/вниз
        cameraHolder.eulerAngles = new Vector3(lookRotation, cameraHolder.eulerAngles.y, cameraHolder.eulerAngles.z);

        if (isDead) return;

        // Повернуть игрока в сторону взгляда
        transform.Rotate(Vector3.up * look.x * sensitivity);
    }

    // Прыжок
    private void Jump()
    {
        rBody.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
    }

    // Вызов ввода передвижения
    public void OnMove(InputAction.CallbackContext context)
    {
        move = context.ReadValue<Vector2>();
        animator.SetBool("isRunning", (move.x != 0) || (move.y != 0));
    }

    // Вызов ввода направления взгляда
    public void OnLook(InputAction.CallbackContext context)
    {
        look = context.ReadValue<Vector2>();
    }

    // Вызов ввода прыжка
    public void OnJump(InputAction.CallbackContext context)
    {
        if (grounded && !isDead)
        {
            Jump();
        }
    }

    // Метод, который будет выполняться на клиенте при обновлении значения на сервере
    public void SyncNickname(string oldValue, string newValue)
    {
        textName.text = playerName;
    }

    [Command]   // Вызвать метод на клиенте, чтобы выполнить код на сервере
    public void CmdSetHP(int _HP)   // Информация игрока направляется на сервер, затем сервер обновляет sync vars на всех клиентах
    {
        HP = _HP;
    }

    // Обновляет хп и удаляет объект
    [Command]
    public void CmdSetHPAndDestroySource(int _HP, GameObject source)
    {
        NetworkServer.Destroy(source);
        HP = _HP;
    }

    // Обновляет хп и состояние смерти
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

    // Вызывает респавн игрока через указанное количество секунд
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
