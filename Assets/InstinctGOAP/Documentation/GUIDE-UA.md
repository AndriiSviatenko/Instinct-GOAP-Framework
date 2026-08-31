# Instinct.GOAP — Гайд українською

> Цей гайд описує **Command API** — низькорівневий шар (`GoapAgent<TCommand>` + StateProvider + Executor).
> Високорівневий **Flow API** (`GoapDomainBuilder`, `GoapBrain`, прив'язки фактів, async-дії) описаний
> англійською в [02-quickstart.md](02-quickstart.md) і коротко в розділі 15 нижче.
> Повна документація: [README.md](README.md) · офлайн-версія однією сторінкою: [index.html](index.html).

## Що таке GOAP?

Goal-Oriented Action Planning — архітектура AI, де агент сам будує план з дій, щоб досягти мети. Вперше в F.E.A.R. (2005).

Відмінність від FSM/BT:
- **FSM:** прописані переходи між станами (100+ станів = хаос)
- **Behavior Tree:** ієрархія умов/дій (гнучко, але послідовності хардкодяться)
- **GOAP:** агент сам знаходить послідовність дій на основі поточного стану

---

## 1. Факти (Fact<T>)

Оголошення:

```csharp
public sealed class Facts
{
    public static readonly Fact<bool>  PlayerVisible = Fact<bool>.Declare();
    public static readonly Fact<int>   Health        = Fact<int>.Declare();
    public static readonly Fact<float> Distance      = Fact<float>.Declare();
}
```

- Доступні типи: `bool`, `int`, `float`, **і будь-який enum** з базовим типом до 32 біт
  (`Fact<Alert>.Declare()`; enum зберігається як число, тому `Compare.GreaterOrEqual` працює по-справжньому)
- `Declare()` сам бере ім'я з назви поля
- **ВАЖЛИВО:** class має бути `sealed class`, не `static class` (C# не дозволяє static як generic-аргумент, але `WorldState.For(typeof(StaticClass))` працює)

---

## 2. WorldState (стан світу)

```csharp
// Створити
var state = WorldState.For<Facts>();

// Записати (fluent)
state.Set(Facts.PlayerVisible, true)
     .Set(Facts.Health, 100);

// Прочитати
bool visible = state.Get(Facts.PlayerVisible);

// Перевірити чи встановлено
bool has = state.Has(Facts.PlayerVisible);

// Клонувати (планувальник робить це сам)
var clone = state.Clone();
```

WorldState — **mutable**. Планувальник заморожує стани через `Freeze()`.

---

## 3. Дії (ActionBuilder.Create())

### Проста дія

```csharp
var pickKey = ActionBuilder.Create()
    .Require(Facts.HasKey, false)         // precondition
    .Effect(Facts.HasKey, true)            // effect
    .Cost(1f)                              // ціна
    .Build();
```

### Preconditions — види порівнянь

```csharp
.Require(Facts.HasKey, true)                     // Equal (за замовчуванням)
.Require(Facts.Health, Compare.Greater, 0)       // >
.Require(Facts.Health, Compare.LessOrEqual, 100) // <=
.Require(Facts.Health, Compare.NotEqual, 0)      // !=
.Require(s => s.Get(Facts.Health) > 0, "живий")  // кастомний предикат
```

### Ключі — ідентичність дій і цілей

```csharp
public static class MyActionKeys
{
    public static readonly ActionKey PickKey  = ActionKey.Declare();
    public static readonly ActionKey OpenDoor = ActionKey.Declare();
    public static readonly ActionKey Exit     = ActionKey.Declare();

    // Якщо дія — власний клас, оголошувати нічого не треба:
    public static readonly ActionKey Chase    = ActionKey.Of<ChaseAction>();
}

public static class MyGoalKeys
{
    public static readonly GoalKey Escape = GoalKey.Declare();
}
```

Порівняння ключа — це порівняння int, а не рядка. Властивості `Name` у `IAction`/`IGoal`
навмисно немає: інакше зʼявляється `action.Name == "Chase"`, яке переживає перейменування
і мовчки перестає працювати. Для логів є `action.NameOf()` / `goal.NameOf()`.

Якщо передати клас ключів у `DomainBuilder.DeclaredGoalsIn(typeof(MyGoalKeys))`, валідація
додатково скаже про ціль, яку оголосили, але забули додати в домен — вона ніколи не
спрацює, і без цієї перевірки це не видно ніяк.

### Effects — види

```csharp
.Effect(Facts.HasKey, true)                           // просте присвоєння
.Copy(Facts.CurrentRoom, Facts.TargetRoom)             // копіювання
.Add(Facts.TripCount, 1, max: 10)                     // інкремент з клемами
.Computed(Facts.Distance, s => Calc(s))                // обчислений з pre-стану
.DynamicEffect((pre, next) => {                        // кілька фактів за раз
    next.Set(Facts.A, pre.Get(Facts.B));
    next.Set(Facts.C, 42);
})
```

**Синхронні ефекти:** всі ефекти читають pre-стан (до дії) і пишуть у next (клон).
Порядок ефектів не впливає на результат.

### Динамічна ціна (Cost)

```csharp
.Cost((state, ctx) => Vector3.Distance(agentPos, targetPos))
```

Якщо cost = 0 або NaN — дія ігнорується планувальником (клемиться в 0.01f).

---

## 4. Цілі (GoalBuilder.Create())

```csharp
var escapeGoal = GoalBuilder.Create(MyGoalKeys.Escape)
    .Satisfy(Facts.Escaped, true)        // умова досягнення
    .Priority(100f)                       // важливість
    .Heuristic(s => DistToExit(s))        // евристика для A* (не обов'язково)
    .RelevantWhen(s => s.Get(Facts.Health) > 0)
    .Build();
```

### Як GOAP обирає ціль:

1. Для кожної релевантної цілі рахується план (A*)
2. Utility = Priority - TotalCost
3. Обирається ціль з найвищим Utility

**RelevantWhen:** якщо ціль нерелевантна — вона навіть не розглядається.

---

## 5. IWorldStateProvider

Перекладає реальний світ в логічний стан для GOAP:

```csharp
public class MyStateProvider : IWorldStateProvider
{
    private readonly Transform _agent;
    private readonly Transform _player;

    public WorldState GetState()
    {
        float dist = Vector3.Distance(_agent.position, _player.position);

        return WorldState.For<Facts>()
            .Set(Facts.PlayerVisible, dist < 10f)
            .Set(Facts.Health, GetHealth());
    }
}
```

**Викликається кожен тік.** Має бути легким.

---

## 6. IActionExecutor<TCommand>

Міст між планувальником і грою:

```csharp
public struct MyCommand
{
    public string Type;
    public Vector3 Target;
}

public class MyExecutor : IActionExecutor<MyCommand>
{
    public MyCommand Translate(IWorldState state, IAction action, IAgentContext ctx)
    {
        var key = action.Key;
        if (key == MyActionKeys.Chase)  return new MyCommand { Type = "Move", Target = _player.position };
        if (key == MyActionKeys.PickUp) return new MyCommand { Type = "Move", Target = _weapon.position };
        return new MyCommand { Type = "Idle" };
    }

    public void OnSelected(IWorldState s, IAction a, IAgentContext ctx) { }
    public void OnCompleted(IAction a, IAgentContext ctx, bool success) { }
}
```

---

## 7. GoapAgent<TCommand>

```csharp
var agent = new GoapAgent<MyCommand>(
    new GoapPlanner(maxIterations: 200, maxDepth: 6),  // планувальник
    new IGoal[] { killGoal },                            // цілі
    new IAction[] { search, chase },                     // дії
    new MyStateProvider(...),                            // провайдер стану
    new MyExecutor(...)                                  // виконавець
);
```

### Життєвий цикл:

```csharp
agent.Tick()              // кожен кадр — повертає команду
agent.NotifyActionComplete(true)  // коли дія виконана
agent.ForceReplan()       // примусово перепланувати
```

### Коли агент переплановує:

1. План закінчився
2. Поточна ціль нерелевантна
3. Preconditions поточної дії не виконуються
4. Викликано ForceReplan()
5. IAgentPolicy.ShouldAbandonPlan() повернув true

### Fallback:

Коли жодна ціль не дала плану:

```csharp
agent.Fallback = state =>
{
    return new MyCommand { Type = "Patrol", Target = GetPoint(state.Get(Facts.PatrolIndex)) };
};
```

---

## 8. GoapAgentHost<TCommand> (Unity)

Мінімум коду для MonoBehaviour:

```csharp
public class MyAgent : GoapAgentHost<MyCommand>
{
    private GoapAgent<MyCommand> _agent;
    protected override IGoapAgent<MyCommand> Agent => _agent;

    void Start()
    {
        // створити planner, actions, goals, provider, executor
        _agent = new GoapAgent<MyCommand>(...);
    }

    protected override void ExecuteCommand(MyCommand cmd)
    {
        // виконати команду
    }
}
```

`Update()` викликає `Agent.Tick()` автоматично.
Якщо команда змінилася — викликає `ExecuteCommand(newCmd)`.

---

## 9. IAgentPolicy (липкість / stickiness)

Запобігає смиканню між цілями:

```csharp
public class MyPolicy : IAgentPolicy
{
    public float UtilityBias(IGoal goal, IGoal currentGoal, WorldState state)
    {
        return goal == currentGoal ? 20f : 0f;  // +20 до поточної цілі
    }

    public bool ShouldAbandonPlan(IPlan plan, int step, WorldState state)
    {
        return false;  // ніколи не кидати план
    }

    public void OnPlanCleared(IAgentContext context) { }
}
```

Підключення: `_agent.Policy = new MyPolicy();`

---

## 10. Дебаг (GoapExplain)

```csharp
GoapExplain.Failure(goal, planner.LastFailure)
GoapExplain.Decision(agent.GoalEvaluations, agent.CurrentPlan)
GoapExplain.Applicability(actions, currentState)
GoapExplain.State<Facts>(currentState)
GoapExplain.Chain(agent.CurrentPlan)

agent.GoalEvaluations  // всі оцінки цілей після останнього replan
agent.CurrentGoal      // поточна ціль
agent.CurrentAction    // поточна дія
agent.CurrentPlan      // весь план
```

---

## 11. DomainBuilder — валідація

```csharp
var domain = new DomainBuilder()
    .AddActions(actions)
    .AddGoals(goals);

var issues = domain.Validate();
string report = domain.Describe();  // або null якщо чисто
```

Перевіряє:
- Дублікати назв дій/цілей
- Дії без ефектів (не можуть просувати план)
- Цілі, які ніхто не може задовольнити

---

## 12. Правила проєктування GOAP

1. **Дія без ефекту — не дія.** Кожна дія має змінювати стан.
2. **Cost ≠ 0.** Навіть мінімальна ціна (0.01f) потрібна для коректної роботи A*.
3. **Планувальник не знає реального світу.** Він оперує тільки фактами. Відстань, позиції, таймери — це робота Provider та Executor.
4. **Цілі без Satisfy не працюють.** Планувальник вважає їх "вже досягнутими". Для поведінки "за замовчуванням" використовуй Fallback.
5. **Факти — це стан, не дії.** Не створюй факти "IsSearching" або "IsChasing". Вони мають описувати світ: "PlayerVisible", "HasWeapon".
6. **RelevantWhen** — твій друг. Використовуй щоб вимкнути ціль коли вона не може бути досягнута.

---

## 13. Повний робочий приклад (ключ-двері-вихід)

```csharp
public sealed class Facts
{
    public static readonly Fact<bool> HasKey   = Fact<bool>.Declare();
    public static readonly Fact<bool> DoorOpen = Fact<bool>.Declare();
    public static readonly Fact<bool> Escaped  = Fact<bool>.Declare();
}

// Дії:
var actions = new IAction[]
{
    ActionBuilder.Create(MyActionKeys.PickKey)
        .Require(Facts.HasKey, false)
        .Effect(Facts.HasKey, true)
        .Cost(1f).Build(),

    ActionBuilder.Create(MyActionKeys.OpenDoor)
        .Require(Facts.HasKey, true)
        .Require(Facts.DoorOpen, false)
        .Effect(Facts.DoorOpen, true)
        .Cost(1f).Build(),

    ActionBuilder.Create(MyActionKeys.Exit)
        .Require(Facts.DoorOpen, true)
        .Require(Facts.Escaped, false)
        .Effect(Facts.Escaped, true)
        .Cost(1f).Build()
};

// Ціль:
var goals = new IGoal[]
{
    GoalBuilder.Create(MyGoalKeys.Escape)
        .Satisfy(Facts.Escaped, true)
        .Priority(100f).Build()
};

// Старт:
var start = WorldState.For<Facts>();  // всі false

// План:
var planner = new GoapPlanner(maxIterations: 100, maxDepth: 10);
var plan = planner.BuildPlan(actions, goals[0], start);
// Результат: PickKey → OpenDoor → Exit
```

---

## 14. Типові помилки

| Помилка | Наслідок |
|---------|----------|
| Cost = 0 | Дія ігнорується |
| Дія без Effect | Ніколи не просуває план |
| Ціль без Satisfy | Завжди "вже досягнута" |
| Два різних класи Facts | Факти не сумісні, помилка WorldState.For |
| Effect не змінює стан | Нескінченний цикл в планувальнику |
| Факти оголошені масивом `Fact<bool>[]` | Схема їх не бачить — стан збирається порожнім, і перший `Get` кидає виняток |
| Вікна `RelevantWhen` із щілиною | При значенні всередині щілини жодна ціль не релевантна: агент стоїть, а гра щокадру переплановує |

---

## 15. Flow API — коротко

Той самий планувальник, але без StateProvider, Executor і enum-команди. Дія описує і прогноз для
планера, і власну поведінку; факти прив'язані до світу в обидва боки.

```csharp
var d = GoapDomainBuilder<FarmerContext>.For<FarmerFacts>();

// світ -> факти, і ефекти успішної дії -> назад у світ
d.Bind(FarmerFacts.Energy, c => c.Energy, (c, v) => c.Energy = v);
d.Bind(FarmerFacts.DistanceToField, c => c.DistanceTo(c.Field));   // похідне: тільки читання

d.Use(new WalkToField(), new Harvest(), new Rest());

d.Goal(FarmerGoalKeys.WorkTheField)
    .Satisfy(FarmerFacts.Energy, Compare.LessOrEqual, 15)
    .RelevantWhen(s => s.Get(FarmerFacts.Energy) >= 45)
    .Priority(40);
```

```csharp
public sealed class Harvest : GoapAction<FarmerContext>
{
    protected override void Setup()
    {
        Require(FarmerFacts.DistanceToField, Compare.LessOrEqual, 1f);
        Require(FarmerFacts.Energy, Compare.GreaterOrEqual, 25);
        Add(FarmerFacts.Energy, -25, min: 0, max: 100);
        Add(FarmerFacts.CropsGrown, +1);
        Cost(1f);
    }

    protected override async UniTask Run(FarmerContext c) => await Wait(0.5f);
}
```

Хост: `private void Update() => _brain.Tick();`

Три правила:
- дійшов до кінця `Run` = успіх, `Fail("причина")` = провал;
- переплан скасовує токен, тому кожен `await` просто зупиняється там, де стояв;
- домен будується **окремо на кожного агента** — дії тримають власний стан запуску.

Живий приклад повністю: `Assets/InstinctGOAP/Samples/Farmer`.
