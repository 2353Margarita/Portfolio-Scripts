using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

//-------------------------------- ПОВЕДЕНИЕ NPC - ПОСЕТИТЕЛЬ КАФЕ

public class Cafe_Customer : MonoBehaviour
{
    public static List<Cafe_Customer> AllCustomers = new List<Cafe_Customer>();

    public static int customersCounter = 10;
    [SerializeField] NavMeshAgent nmAgent;
    [SerializeField] Animator animator;
    public Walker_NavMesh walker;
    [SerializeField] Vector2 minMaxDinnerTimeSec = new Vector2(3.0f, 5.0f);
    [SerializeField] Transform itemsRoot;
    [SerializeField] ProductItem trashPrefab;
    [SerializeField] float blendCargoAnimation = 0.0f;
    [SerializeField] float blendOnChairAnimation = 0.0f;

    [Header("Widgets")]
    [SerializeField] GameObject dinnerWidgetGO;
    [SerializeField] UnityEngine.UI.Image dinnerWidgetProgress;

    [Header("Effects")]
    [SerializeField] Game.CustomerEmoji happyEmoji;

    List<TaskStep> behaviour = new List<TaskStep>();
    TaskStep go_to_cafe = new TaskStep();
    TaskStep make_order = new TaskStep();
    TaskStep wait_order = new TaskStep();
    TaskStep go_to_table = new TaskStep();
    TaskStep dinner = new TaskStep();
    TaskStep go_to_door = new TaskStep();
    TaskStep go_to_trashcan = new TaskStep();
    TaskStep go_to_exit = new TaskStep();

    CafeOrder myOrder = null;
    public CafeOrder MyOrder => myOrder;

    Cafe_Chair myPlace = null;
    float timer = 0.0f;
    float dinnerTime = 3.0f;
    List<ProductItem> dishes = new List<ProductItem>();
    bool needUpdateRotation = true;
    public string debug_state = "";


    private void Start()
    {
        nmAgent.avoidancePriority = customersCounter;
        customersCounter++;
        if (customersCounter > 100) customersCounter = 10;
        dinnerWidgetGO.SetActive(false);
        SetUpBaseBehaviour();
    }

    public Vector3 CurrentDestination => walker.CurrentDestination;

    private void OnEnable()
    {
        if (!AllCustomers.Contains(this)) AllCustomers.Add(this);
    }
    private void OnDisable()
    {
        if (AllCustomers.Contains(this)) AllCustomers.Remove(this);
    }

    private void Update()
    {
        behaviour.Process(null);
    }

    void SetUpBaseBehaviour()
    {
        go_to_cafe.start_func = () =>
        {
            debug_state = "go_to_cafe";
            Cafe.Instance.AddCustomer(this);
            walker.StartMovement(Cafe.Instance.CurrentQueuePoint(this));
        };
        go_to_cafe.process_func = () =>
        {
            go_to_cafe.complete = Cafe.Instance.CurrentCustomer() == this;

            if (walker.DestinationIsReached())
            {
                if (needUpdateRotation)
                {
                    walker.LookAtPoint(Cafe.Instance.StartQueuePoint.position);
                    needUpdateRotation = false;
                }

                var new_point = Cafe.Instance.CurrentQueuePoint(this);
                if (!walker.DestinationIs(new_point))
                {
                    debug_state = "go_to_cafe 1";
                    walker.StartMovement(new_point);
                    needUpdateRotation = true;
                    walker.LookAtPoint(Cafe.Instance.StartQueuePoint.position);
                }
            }
        };
        go_to_cafe.end_func = () =>
        {
            walker.enabled = false;
            walker.LookAtVector(-Cafe.Instance.StartQueuePoint.forward);
        };

        make_order.start_func = () =>
        {
            debug_state = "make_order";
            animator.SetTrigger("hello");
            myOrder = null;
        };
        make_order.process_func = () =>
        {
            if (myOrder == null)
            {
                myOrder = Cafe.Instance.GetRandomOrder();
                if (myOrder != null)
                    Cafe.Instance.UpdateOrderWidget();
            }
            make_order.complete = myOrder != null;
        };

        wait_order.start_func = () =>
        {
            debug_state = "wait_order";
        };
        wait_order.process_func = () =>
        {
            wait_order.complete = Cafe.Instance.OrderIsReady(MyOrder);
        };
        wait_order.end_func = () =>
        {
            ///оплата, забор заказа ...
            int i;
            foreach (var orderPart in myOrder.products)
            {
                for (i = 0; i < orderPart.count; i++)
                {
                    var product = Cafe.Instance.OrderStock.RemoveLastOfType(orderPart.product);
                    product.transform.SetParent(itemsRoot);
                    product.transform.localPosition = Vector3.zero;
                    dishes.Add(product);
                }
            }
            Cafe.Instance.RemoveCustomer(this);
            animator.SetFloat("walk_blend", blendCargoAnimation);
            Cafe.Instance.SpawnCheckoutPopup((ulong)myOrder.price);
            if (happyEmoji != null) happyEmoji.SetActive(true);

            myPlace = Cafe.Instance.GetFreePlace();
            if (myPlace == null)
            {
                animator.SetFloat("walk_blend", blendCargoAnimation);
                go_to_table.ForceRady();
                dinner.ForceRady();
            }
            else
            {
                myPlace.free = false;
            }
        };

        go_to_table.start_func = () =>
        {
            debug_state = "go_to_table";
            walker.enabled = true;
            walker.StartMovement(myPlace.pointForCharacter.position);
            animator.SetFloat("walk_blend", blendCargoAnimation);
        };
        go_to_table.process_func = () =>
        {
            go_to_table.complete = walker.DestinationIsReached();
        };
        go_to_table.end_func = () =>
        {
            transform.position = myPlace.pointForCharacter.position;
            walker.LookAtVector(myPlace.pointForCharacter.forward);
            animator.SetFloat("walk_blend", 0.0f);
        };

        dinner.start_func = () =>
        {
            debug_state = "dinner";
            walker.enabled = false;
            animator.SetBool("dinner", true);
            dinnerWidgetGO.SetActive(true);
            timer = 0.0f;
            dinnerTime = Random.Range(minMaxDinnerTimeSec.x, minMaxDinnerTimeSec.y);
            foreach (var dish in dishes)
            {
                dish.transform.SetParent(myPlace.pointOnTable);
                dish.transform.localPosition = Vector3.zero;
                var euler = dish.transform.eulerAngles;
                euler.x = euler.z = 0.0f;
                dish.transform.eulerAngles = euler;
            }
            animator.SetFloat("idle_blend", blendOnChairAnimation);
        };
        dinner.process_func = () =>
        {
            timer += Time.deltaTime;
            dinnerWidgetProgress.fillAmount = timer / dinnerTime;
            dinner.complete = timer >= dinnerTime;
        };
        dinner.end_func = () =>
        {
            animator.SetBool("dinner", false);
            dinnerWidgetGO.SetActive(false);
            int i;
            for (i = 0; i < dishes.Count; i++)
                Destroy(dishes[i].gameObject);
            dishes.Clear();
            var trash = Pooler.Instance.GetObject(trashPrefab);
            trash.transform.SetParent(myPlace.pointOnTable);
            trash.transform.localPosition = Vector3.zero;
            myPlace.clean = false;
            myPlace.free = true;
            animator.SetFloat("idle_blend", 0.0f);
        };

        go_to_door.start_func = () =>
        {
            debug_state = "go_to_door";
            walker.enabled = true;
            walker.StartMovement(Cafe.Instance.exitDoorPoint.position);
        };
        go_to_door.process_func = () =>
        {
            go_to_door.complete = walker.DestinationIsReached();
        };
        go_to_door.end_func = () =>
        {
            if (happyEmoji != null) happyEmoji.SetActive(true);
        };

        go_to_trashcan.start_func = () =>
        {
            debug_state = "go_to_trashcan";
            walker.enabled = true;
            walker.StartMovement(Cafe.Instance.TrashcanPoint.position);
        };
        go_to_trashcan.process_func = () =>
        {
            go_to_trashcan.complete = walker.DestinationIsReached();
        };
        go_to_trashcan.end_func = () =>
        {
            walker.LookAtVector(Cafe.Instance.TrashcanPoint.forward);
            foreach (var dish in dishes)
            {
                Destroy(dish.gameObject);
            }
            var trash = Instantiate(trashPrefab);
            trash.transform.position = itemsRoot.position;
            Cafe.Instance.TrashcanStock.AddItem(trash);
            animator.SetFloat("walk_blend", 0.0f);
        };

        go_to_exit.start_func = () =>
        {
            debug_state = "go_to_exit";
            walker.enabled = true;
            walker.StartMovement(Cafe.Instance.exitPoint.position);
        };
        go_to_exit.process_func = () =>
        {
            go_to_exit.complete = walker.DestinationIsReached();
        };
        go_to_exit.end_func = () =>
        {
            Destroy(gameObject);
        };

        behaviour.Clear();
        behaviour.Add(go_to_cafe);// движение к кассе, становимся в очередь
        behaviour.Add(make_order);// создание заказа
        behaviour.Add(wait_order);// ожидание заказа
        behaviour.Add(go_to_table);// движение к столику, если есть свободное место
        behaviour.Add(dinner);// приём пищи
        behaviour.Add(go_to_door);// движение к двери, радостный смайлик
        behaviour.Add(go_to_trashcan);// движение к мусорным бакам
        behaviour.Add(go_to_exit);// движение на выход, удаление
    }
}
