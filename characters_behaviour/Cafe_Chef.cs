using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

//-------------------------------- ПОВЕДЕНИЕ NPC - ПОВАР

public class Cafe_Chef : MonoBehaviour
{
    public static List<Cafe_Chef> AllChefs = new List<Cafe_Chef>();
    public static int chefsCounter = 10;
    [SerializeField] NavMeshAgent nmAgent;
    [SerializeField] Animator animator;
    public Walker_NavMesh walker;
    [SerializeField] Vector2 minMaxCookTimeSec = new Vector2(3.0f, 5.0f);
    [SerializeField] Transform itemsRoot;
    [SerializeField] float blendCargoAnimation = 0.0f;
    [SerializeField] float blendCookAnimation = 0.0f;
    [Header("Widgets")]
    [SerializeField] UI_TaskWidget taskWidget;
    [SerializeField] Sprite cookPlaceIcon;
    [SerializeField] Sprite cookProcessIcon;
    [SerializeField] Sprite checkoutIcon;

    [Header("Effects")]
    [SerializeField] ParticleSystem restPS;

    List<TaskStep> behaviour = new List<TaskStep>();
    TaskStep wait_customer_order = new TaskStep();
    TaskStep go_to_ingredient = new TaskStep();
    TaskStep go_to_cook_place = new TaskStep();
    TaskStep cook = new TaskStep();
    TaskStep go_to_checkout = new TaskStep();
    TaskStep go_to_rest_point = new TaskStep();

    float timer = 0.0f;
    float cookTime = 3.0f;
    public string debug_state = "";
    CafeOrderLine processedOrederPart = null;
    Stock targetStock = null;
    ProductItem currentIngredient = null;
    ProductItem currentDish = null;
    Cafe_Chair cookPlace = null;
    Vector3 restPoint;
    bool isRest = true;
    float startSpeed = 0.0f;


    private void Start()
    {
        nmAgent.avoidancePriority = chefsCounter;
        chefsCounter++;
        if (chefsCounter > 100) chefsCounter = 10;
        taskWidget.gameObject.SetActive(false);
        if (restPS != null) restPS.playOnAwake = false;
        SetUpBaseBehaviour();
        restPoint = transform.position;
        isRest = true;
        UpdateSkills();
    }

    private void OnEnable()
    {
        if (!AllChefs.Contains(this)) AllChefs.Add(this);
    }
    private void OnDisable()
    {
        if (AllChefs.Contains(this)) AllChefs.Remove(this);
    }

    public Vector3 CurrentDestination => walker.CurrentDestination;

    private void Update()
    {
        behaviour.Process(null);
    }

    void SetUpBaseBehaviour()
    {
        wait_customer_order.start_func = () =>
        {
            debug_state = "wait_customer_order";
            if (restPS != null) restPS.Play();
            isRest = true;
        };
        wait_customer_order.process_func = () =>
        {
            if (Cafe.Instance.CurrentCustomer() != null && Cafe.Instance.TargetOrder != null)
            {
                FindTask();
                if (processedOrederPart != null)
                {
                    var ingredient = Cafe.Instance.GetIngredient(processedOrederPart.product);
                    if (ingredient != null)
                    {
                        targetStock = Cafe.Instance.StockForProduct(ingredient.Product);
                        taskWidget.taskIcon.sprite = ingredient.Product.Icon;
                    }
                }
            }
            wait_customer_order.ready = processedOrederPart != null && processedOrederPart.count != 0 && targetStock != null;
        };
        wait_customer_order.end_func = () =>
        {
            if (restPS != null) restPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            isRest = false;
        };

        go_to_ingredient.start_func = () =>
        {
            debug_state = "go_to_product";
            walker.enabled = true;
            walker.StartMovement(Cafe.Instance.PointStockForChef(targetStock));
            taskWidget.gameObject.SetActive(true);
            taskWidget.progressImage.fillAmount = 0.0f;
            ///taskWidget.taskIcon.sprite = processedOrederPart.product.Icon;
        };
        go_to_ingredient.process_func = () =>
        {
            go_to_ingredient.ready = walker.DestinationIsReached() && targetStock.Count > 0;
        };
        go_to_ingredient.end_func = () =>
        {
            currentIngredient = targetStock.RemoveLast();
            targetStock = null;
            currentIngredient.transform.SetParent(itemsRoot);
            currentIngredient.transform.localPosition = Vector3.zero;
        };

        go_to_cook_place.start_func = () =>
        {
            debug_state = "go_to_cook_place";
            walker.enabled = true;
            taskWidget.taskIcon.sprite = cookPlaceIcon;
            animator.SetFloat("walk_blend", blendCargoAnimation);
        };
        go_to_cook_place.process_func = () =>
        {
            if (cookPlace == null)
            {
                cookPlace = Cafe.Instance.GetFreeCookPlace();
                if (cookPlace != null)
                {
                    walker.StartMovement(cookPlace.pointForCharacter.position);
                    cookPlace.free = false;
                }
            }
            go_to_cook_place.ready = cookPlace != null && walker.DestinationIsReached();
        };
        go_to_cook_place.end_func = () =>
        {
            walker.enabled = false;
        };

        cook.start_func = () =>
        {
            debug_state = "cook";
            cookTime = Random.Range(minMaxCookTimeSec.x, minMaxCookTimeSec.y);
            timer = 0.0f;
            currentIngredient.transform.SetParent(null);
            currentIngredient.transform.position = cookPlace.pointOnTable.position;
            taskWidget.taskIcon.sprite = cookProcessIcon;
            walker.LookAtVector(cookPlace.pointOnTable.position - cookPlace.pointForCharacter.position);
            animator.SetFloat("idle_blend", blendCookAnimation);
        };
        cook.process_func = () =>
        {
            timer += Time.deltaTime;
            taskWidget.progressImage.fillAmount = timer / cookTime;
            cook.ready = timer >= cookTime;
        };
        cook.end_func = () =>
        {
            var dishPrefab = Cafe.Instance.GetDish(currentIngredient.Product);
            if (dishPrefab == null) currentDish = currentIngredient;
            else
            {
                currentDish = Pooler.Instance.GetObject(dishPrefab.gameObject).gameObject.GetComponent<ProductItem>();
                currentDish.transform.SetParent(itemsRoot);
                currentDish.transform.localPosition = Vector3.zero;
                Destroy(currentIngredient.gameObject);
            }
            currentIngredient = null;
            cookPlace.free = true;
            cookPlace = null;
            animator.SetFloat("idle_blend", 0.0f);
        };

        go_to_checkout.start_func = () =>
        {
            debug_state = "go_to_checkout";
            walker.enabled = true;
            walker.StartMovement(Cafe.Instance.CashierPoint.position);
            taskWidget.progressImage.fillAmount = 0.0f;
            taskWidget.taskIcon.sprite = checkoutIcon;
        };
        go_to_checkout.process_func = () =>
        {
            go_to_checkout.ready = walker.DestinationIsReached();
        };
        go_to_checkout.end_func = () =>
        {
            Cafe.Instance.OrderStock.AddItem(currentDish);
            Cafe.Instance.completedOrderParts.products.AddProduct(processedOrederPart);
            processedOrederPart = null;
            if (Cafe.Instance.OrderIsReady(Cafe.Instance.TargetOrder))// если этот повар принёс последнюю часть заказа, он остаётся у кассы
            {
                restPoint = Cafe.Instance.CashierPoint.position;
                walker.enabled = false;
                behaviour.ResetAll();
            }
            currentDish = null;
            taskWidget.gameObject.SetActive(false);
            isRest = true;
            animator.SetFloat("walk_blend", 0.0f);
        };

        go_to_rest_point.start_func = () =>
        {
            debug_state = "go_to_rest_point";
            walker.enabled = true;
            SelectRestPoint();
            walker.StartMovement(restPoint);
        };
        go_to_rest_point.process_func = () =>
        {
            go_to_rest_point.ready = walker.DestinationIsReached();
        };
        go_to_rest_point.end_func = () =>
        {
            walker.enabled = false;
            behaviour.ResetAll();
            walker.LookAtPoint(Cafe.Instance.CashierPoint.position);
        };


        behaviour.Clear();
        behaviour.Add(wait_customer_order); // ожидание клиента и заказа
        behaviour.Add(go_to_ingredient);// движение к ингредиентам
        behaviour.Add(go_to_cook_place);// движение к месту готовки
        behaviour.Add(cook);// готовка
        behaviour.Add(go_to_checkout);// движение к кассе, выдача части заказа
        behaviour.Add(go_to_rest_point);// движение к точке отдыха
    }

    void FindTask()
    {
        if (Cafe.Instance.TargetOrder == null) return;

        foreach (var chef in AllChefs)
            if (chef.isRest && chef != this && chef.restPoint == Cafe.Instance.CashierPoint.position)// если кто-то стоит на кассе, то он первым берёт задание
                return;

        CafeOrder completedPart = new CafeOrder();
        completedPart.AddProducts(Cafe.Instance.completedOrderParts);
        foreach (var chef in AllChefs)
            if (chef != this && chef.processedOrederPart != null)
                completedPart.products.AddProduct(chef.processedOrederPart);
        if (completedPart.ContainsAllProducts(Cafe.Instance.TargetOrder))
            return;
        else
        {
            foreach (var need in Cafe.Instance.TargetOrder.products)
            {
                if (need.count > completedPart.products.GetCount(need.product))
                {
                    processedOrederPart = new CafeOrderLine(need.product, 1);
                }
            }
        }
    }

    void SelectRestPoint()
    {
        List<Vector3> points = new List<Vector3>();
        foreach (var point in Cafe.Instance.RestPointsForChefs)
            points.Add(point.position);
        foreach (var chef in AllChefs)
            if (chef != this && chef.isRest && points.Contains(chef.restPoint))
                points.Remove(chef.restPoint);
        if (points.Count > 0)
        {
            restPoint = points[Random.Range(0, points.Count)];
        }
        else
        {
            restPoint = Cafe.Instance.RestPointsForChefs[Random.Range(0, Cafe.Instance.RestPointsForChefs.Count)].position;
        }
    }

    public void SetupPosition()
    {
        SelectRestPoint();
        walker.SetPosition(restPoint);
    }

    public void UpdateSkills()
    {
        if (startSpeed == 0.0f) startSpeed = nmAgent.speed;
        if (Cafe.Instance != null)
        {
            nmAgent.speed = startSpeed * Cafe.Instance.GetSpeedModifier();
        }
    }
}
