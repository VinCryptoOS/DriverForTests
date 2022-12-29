using System.Collections.Concurrent;
using System.Diagnostics;

namespace DriverForTestsLib;

/// <summary>Класс описывает ошибку, возникшую в ходе теста</summary>
public class TestError
{
    public Exception? ex = null;
    public string     Message = "";
}

/// <summary>Этот класс должен быть переопределён потомком.
/// Он создаёт список нефильтрованных задач и определяет условия их фильтрации</summary>
public abstract class TestConstructor
{
                                                                                /// <summary>Если true, то после выполнения тестов программа будет ждать нажатия Enter [Console.ReadLine()]</summary>
    public bool                  Console_ReadLine = false;                      /// <summary>Процессу будет присвоен приоритет ProcessPriority при старте задач. Может быть null</summary>
    public ProcessPriorityClass? ProcessPriority  = null;                       /// <summary>Условие на выполнение задач</summary>
    public TestTaskTagCondition? conditions;                                    /// <summary>Общий приоритет на выполнение.<para>Если у задачи нет хотя бы одного тега с приоритетом не менее generalPriorityForTasks, то она будет пропущена. Задачи без тегов выполняются</para></summary>
    public double                generalPriorityForTasks = double.MinValue;

    /// <summary>Метод, заполняющий нефильтрованный список задач для выполнения в тестах</summary>
    /// <param name="tasks">Список для заполнения задачами</param>
    public abstract void CreateTasksLists(ConcurrentQueue<TestTask> tasks);

    /// <summary>Определяет, нужно ли запускать эту задачу в зависимости от тегов и this.generalPriorityForTasks</summary>
    /// <returns>true - задачу нужно запускать</returns>
    public virtual bool ShouldBeExecuted(TestTask task)
    {
        if (!task.isSatisfiesThePriority(generalPriorityForTasks))
            return false;

        if (conditions == null)
            return true;

        return conditions.isSatisfiesForTask(task);
    }

    /// <summary>Этот метод используется для сообщения в вызывающую программу о том,
    //  что при автоматической постановке задач выявлена невозможность её постановки</summary>
    /// <param name="TaskType">Тип тестовой задачи, которую метод пытался поставить</param>
    /// <param name="notAutomatic">Если true, то в одном из атрибутов задачи установлен флаг notAutomatic, то есть она штатно не должна добавляться автоматически</param>
    public delegate void ErrorTaskHandler(Type TaskType, bool notAutomatic);

    /// <summary>Это статический метод, который получает тестовые задачи из всех загруженных сборок</summary>
    /// <param name="errorHandler">Обработчик задач, которые автоматически не могут быть получены</param>
    /// <returns>Возвращает список задач, которые можно вручную добавить в список на выполнение (смотреть addTasksForQueue)</returns>
    public static List<TestTask> getTasksFromAppDomain(ErrorTaskHandler? errorHandler)
    {
        var result = new List<TestTask>(16);

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        //  foreach (var assembly in assemblies)
        Parallel.ForEach<System.Reflection.Assembly>
        (
            assemblies,
            (System.Reflection.Assembly assembly, ParallelLoopState state, long index)
            =>
            
            {
                foreach (var type in assembly.GetTypes())
                {
                    // Получаем все атрибуты TestTagAttribute, которые выставлены на тип
                    var attributes = (  TestTagAttribute []  )
                                type.GetCustomAttributes(typeof(TestTagAttribute), true);

                    // Если таких атрибутов нет, значит тип нам не интересен
                    if (attributes.Length <= 0)
                        continue;

                    // Проверяем, что тип является пригодным для автоматической постановки задачи
                    var notAutomatic = false;
                    foreach (var attribute in attributes)
                    {
                        if (attribute.notAutomatic)
                        {
                            notAutomatic = true;
                            errorHandler?.Invoke(type, notAutomatic);
                            break;
                        }
                    }

                    if (notAutomatic)
                        continue;

                    // Ищем конструтор по умолчанию. Если его нет, сообщаем об ошибке
                    var ci = type.GetConstructor(  new Type[] {}  );
                    if (ci == null)
                    {
                        errorHandler?.Invoke(type, false);

                        continue;
                    }

                    // Создаём экземпляр тестовой задачи конструктором по умолчанию
                    var t = ci?.Invoke(new object[] {});
                    if (t is not null)
                    {
                        var task = (TestTask) t;

                        lock (result)
                            result.Add(task);
                    }
                }
            }
        );

        return result;
    }

    /// <summary>Добавляет задачи, полученные с помощью getTasksFromAppDomain</summary>
    /// <param name="source">Исходный список задач (например, из getTasksFromAppDomain)</param><param name="tasksQueue">Список, куда будем добавлять задачи</param>
    public static void addTasksForQueue(IEnumerable<TestTask> source, ConcurrentQueue<TestTask> tasksQueue)
    {
        foreach (var task in source)
            tasksQueue.Enqueue(task);
    }
}

/// <summary>Описывает тег задачи</summary>
public class TestTaskTag
{
                                                                        /// <summary>Имя тега</summary>
    public readonly string name;                                        /// <summary>Приоритет тега: чем больше, тем выше приоритет</summary>
    public readonly double priority = 0.0d;

    /// <param name="tagName">Имя тега</param>
    /// <param name="tagPriority">Приоритет тега</param>
    public TestTaskTag(string tagName, double tagPriority)
    {
        name     = tagName;
        priority = tagPriority;
    }
}

/// <summary>Описывает условие на выполнение тестовых задач</summary>
public class TestTaskTagCondition
{
    /// <summary>Список задач связан следующими операторами
    /// <para>And - все теги из списка должны присутствовать в задаче; используется listOfNeedTags</para>
    /// <para>Count - должно присутствовать не менее countForConditionOperator тегов; используется listOfNeedTags</para>
    /// <para>TreeAnd и TreeCount - необходимо выполнение условий из listOfNeedConditions; используется listOfNeedConditions; условия аналогичный And и Count</para>
    /// </summary>
    /// <remarks>Допустимо только одно из значений</remarks>
    public enum ConditionOperator { Error = 0, And = 1, Count = 2, TreeAnd = 4, TreeCount = 8, AlwaysTrue = 16, AlwaysFalse = 32, TreePriority = 64 };

                                                                            /// <summary>Список тегов, участвующих в условии. Используется при операторах And и Count</summary>
    public List<TestTaskTag>? listOfNeedTags;                               /// <summary>Оператор, который будет применён к тегам (and, count, tree)</summary>
    public ConditionOperator  conditionOperator;                            /// <summary>Необходимое количество повторов для операторов Count и TreeCount; при использовании этого оператора должно быть больше 0</summary>
    public Int64              countForConditionOperator;                    /// <summary>Результат вычислений подвергается логическому отрицанию</summary>
    public bool               isReversedCondition = false;                  /// <summary>Если этому правилу соответствует задача, то она вызовет срабатывание false на всём условии вне зависимости от оператора</summary>
    public bool               isMandatoryExcludingRule = false;             /// <summary>Побеждает задача с большим приоритетом; только для TreePriority. Для одинакового приоритета - and</summary>
    public double             priorityForCondition = double.MinValue;

    /// <summary>Список подусловий, участвующих в этом условии. Используется с операторами TreeAnd, TreeCount, TreePriority</summary>
    public List<TestTaskTagCondition>? listOfNeedConditions;

    /// <summary>Проверяет, удовлетворяет ли задача task этому условию</summary>
    /// <param name="task">Проверяемая задача</param><returns>true, если задача удовлетворяет этому условию</returns>
    public virtual bool isSatisfiesForTask(TestTask task)
    {
        return isSatisfiesForTask_withoutReverse(task) ^ isReversedCondition;
    }

    /// <summary>Проверка, аналогичная isSatisfiesForTask, но без учёта isReversedCondition</summary>
    /// <param name="task">Проверяемая задача</param><returns>true, если задача удовлетворяет этому условию без учёта isReversedCondition</returns>
    protected virtual bool isSatisfiesForTask_withoutReverse(TestTask task)
    {
        switch (conditionOperator)
        {
            case ConditionOperator.And:
                return isSatisfiesForTask_And(task);

            case ConditionOperator.Count:
                return isSatisfiesForTask_Count(task);

            case ConditionOperator.TreeAnd:
                return isSatisfiesForTask_TreeAnd(task);

            case ConditionOperator.TreeCount:
                return isSatisfiesForTask_TreeCount(task);

            case ConditionOperator.AlwaysTrue:
                return true;
            
            case ConditionOperator.AlwaysFalse:
                return false;

            case ConditionOperator.TreePriority:
                return isSatisfiesForTask_TreePriority(task);

            default:
                throw new Exception($"TestTaskTagCondition.isSatisfiesForTask: Illegal value of conditionOperator: {(int) conditionOperator}");
        }
    }

    /// <summary>Проверка оператора And</summary>
    /// <param name="task">Задача для проверки</param><returns>true, если задача удовлетворяет этому условию без учёта isReversedCondition</returns>
    protected virtual bool isSatisfiesForTask_And(TestTask task)
    {
        if (listOfNeedTags == null)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_And: {nameof(listOfNeedTags)} == null");

        foreach (var tag in listOfNeedTags)
        {
            if (!task.isSatisfiesTag(tag))
                return false;
        }

        return true;
    }

    /// <summary>Проверка оператора Count</summary>
    /// <param name="task">Задача для проверки</param><returns>true, если задача удовлетворяет этому условию без учёта isReversedCondition</returns>
    protected virtual bool isSatisfiesForTask_Count(TestTask task)
    {
        if (listOfNeedTags == null)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_Count: {nameof(listOfNeedTags)} == null");
        if (countForConditionOperator <= 0)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_Count: {nameof(countForConditionOperator)} <= 0");

        var cnt = 0;
        foreach (var tag in listOfNeedTags)
        {
            if (task.isSatisfiesTag(tag))
                cnt++;
        }

        return cnt >= countForConditionOperator;
    }

    /// <summary>Проверка оператора TreeAnd</summary>
    /// <param name="task">Задача для проверки</param><returns>true, если задача удовлетворяет этому условию без учёта isReversedCondition</returns>
    protected virtual bool isSatisfiesForTask_TreeAnd(TestTask task)
    {
        if (listOfNeedConditions == null)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_And: {nameof(listOfNeedConditions)} == null");

        foreach (var condition in listOfNeedConditions)
        {
            if (condition.isMandatoryExcludingRule)
            {
                if (condition.isSatisfiesForTask(task))
                    return false;

                continue;
            }

            if (!condition.isSatisfiesForTask(task))
                return false;
        }

        return true;
    }

    /// <summary>Проверка оператора TreeCount</summary>
    /// <param name="task">Задача для проверки</param><returns>true, если задача удовлетворяет этому условию без учёта isReversedCondition</returns>
    protected virtual bool isSatisfiesForTask_TreeCount(TestTask task)
    {
        if (listOfNeedConditions == null)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_Count: {nameof(listOfNeedTags)} == null");
        if (countForConditionOperator <= 0)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_Count: {nameof(countForConditionOperator)} <= 0");

        var cnt = 0;
        foreach (var condition in listOfNeedConditions)
        {
            if (condition.isMandatoryExcludingRule)
            {
                if (condition.isSatisfiesForTask(task))
                    return false;

                continue;
            }

            if (condition.isSatisfiesForTask(task))
                cnt++;
        }

        return cnt >= countForConditionOperator;
    }

    /// <summary>Проверка оператора TreePriority</summary>
    /// <param name="task">Задача для проверки</param><returns>true, если задача удовлетворяет этому условию без учёта isReversedCondition</returns>
    protected virtual bool isSatisfiesForTask_TreePriority(TestTask task)
    {
        if (listOfNeedConditions == null)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_TreePriority: {nameof(listOfNeedTags)} == null");

        var result = true;
        var curP   = double.MinValue;
        foreach (var condition in listOfNeedConditions)
        {
            if (condition.isMandatoryExcludingRule)
            {
                if (condition.isSatisfiesForTask(task))
                    return false;

                continue;
            }

            if (condition.priorityForCondition > curP)
            {
                result = condition.isSatisfiesForTask(task);
            }
            else
            if (condition.priorityForCondition == curP)
            {
                if (!condition.isSatisfiesForTask(task))
                    result = false;
            }
        }

        return result;
    }
}

/// <summary>Класс тестовых задач. Должен быть переопределён каждым конкретным тестом</summary>
public abstract class TestTask
{
    /// <summary>Определение типа делегата для вызова конкретной задачи</summary>
    public delegate void TestTaskFn();

    /// <param name="Name">Имя задачи: может быть не уникальным, однако, для идентификации задач в логе рекомендуется уникальное имя</param>
    public TestTask(string Name)
    {
        this.Name = Name;
        this.taskFunc = () => { throw new NotImplementedException(); };

        var attributes = (  TestTagAttribute []  )
                         this.GetType().GetCustomAttributes(typeof(TestTagAttribute), true);

        // Применение атрибутов к задаче
        foreach (var attribute in attributes)
        {
            // Добавляем теги из атрибутов
            if (attribute.tag is not null)
                tags.Add(attribute.tag);

            // Устанавливаем флаг экслюзивной задачи, который выделен весь процессор целиком
            if (attribute.singleThread)
            {
                waitBefore = true;
                waitAfter  = true;
            }
        }
    }
                                                                    /// <summary>Каким тегам удовлетворяет задача</summary>
    public List<TestTaskTag> tags = new List<TestTaskTag>();

                                                                    /// <summary>Функция тестирования, которая вызывается библиотекой</summary>
    public          TestTaskFn  taskFunc {get; protected set;}      /// <summary>Имя задачи</summary>
    public          string      Name     {get; protected set;}      /// <summary>Если true, то задача стартовала (остаётся true навсегда)</summary>
    public          bool        start = false;                      /// <summary>Если true, то задача завершена (в том числе, с исключением)</summary>
    public          bool        ended = false;                      /// <summary>Список ошибок, возникших при исполнении данной тестовой задачи</summary>
    public readonly List<TestError> error = new List<TestError>();
                                                                    /// <summary>Время старта задачи</summary>
    public DateTime started = default;                              /// <summary>Время завершения задачи (в том числе, по исключению)</summary>
    public DateTime endTime = default;

                                                                    /// <summary>Перед выполнением этой задачи программа ждёт завершения всех предыдущих задач</summary>
    public bool waitBefore = false;                                 /// <summary>После постановки задачи программа ждёт завершения этой задачи (не ставит другие задачи)</summary>
    public bool waitAfter  = false;

    /// <summary>Выполнение задачи в процентах (0-100)</summary><remarks>Задача может не использовать этот параметр</remarks>
    public float done = 0f;

    /// <summary>Проверяет, удовлетворяет ли задача указанному приоритету</summary>
    /// <param name="generalPriorityForTasks">Заданный приоритет</param>
    /// <returns>true, если нет тегов вообще и или есть хоть один тег с приоритетом не менее generalPriorityForTasks</returns>
    public virtual bool isSatisfiesThePriority(double generalPriorityForTasks)
    {
        if (tags.Count <= 0)
            return true;

        foreach (var tag in tags)
        {
            if (tag.priority >= generalPriorityForTasks)
                return true;
        }

        return false;
    }

    /// <summary>Определяет, удовлетворяет ли задача заданному тегу с учётом указанного приоритета</summary>
    /// <param name="tag">Заданный тег, которому должна удовлетворять задача</param>
    /// <returns>true, если задача удовлетворяет тегу</returns>
    public virtual bool isSatisfiesTag(TestTaskTag tag)
    {
        foreach (var t in tags)
        {
            if (t.name != tag.name)
                continue;

            if (t.priority >= tag.priority)
                return true;
        }

        return false;
    }
}

/// <summary>Класс определяет атрибут, который навешивается на наследника TestTask.
/// На тестовую задачу вешается тег с соответствующим именем</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class TestTagAttribute: Attribute
{                                                                           /// <summary>Тег, установленный атрибутом (может быть null)</summary>
    public readonly TestTaskTag? tag;                                       /// <summary>Если true, то тестовая задача выполняется одна на всём процессоре (другие тестовые задачи не выполняются в это время)</summary>
    public readonly bool         singleThread = false;                      /// <summary>Если true, то задача не будет автоматически регистрироваться на выполнение функцией TestConstructor.getTasksFromAppDomain (нужно добавить её вручную)</summary>
    public readonly bool         notAutomatic = false;

    /// <param name="tagName">Имя тега (может быть null)</param>
    /// <param name="priority">Приоритет тега</param>
    /// <param name="singleThread">Задача для эксплюзивного выполнения на всём процессоре (другие тестовые задачи не будут выполняться одновременно)</param>
    /// <param name="notAutomatic">Задача не будет автоматически поставлена на выполнение (требуется ручная регистрация)</param>
    public TestTagAttribute(string? tagName = null, double priority = 0d, bool singleThread = false, bool notAutomatic = false)
    {
        if (tagName is not null)
            tag = new TestTaskTag(tagName, priority);

        this.singleThread = singleThread;
        this.notAutomatic = notAutomatic;
    }
}
