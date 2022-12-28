using System.Collections.Concurrent;
using System.Diagnostics;

namespace DriverForTestsLib;

public class Error
{
    public Exception? ex = null;
    public string     Message = "";
}

public abstract class TestConstructor
{
                                                                                /// <summary>Если true, то после выполнения тестов программа будет ждать нажатия Enter [Console.ReadLine()]</summary>
    public bool                  Console_ReadLine = false;                      /// <summary>Процессу будет присвоен приоритет ProcessPriority при старте задач. Может быть null</summary>
    public ProcessPriorityClass? ProcessPriority  = null;                       /// <summary>Условие на выполнение задач</summary>
    public TestTaskTagCondition? conditions;
    public double                generalPriorityForTasks = double.MinValue;

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

    public delegate void ErrorTaskHandler(Type TaskType, bool notAutomatic);
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
                    var attributes = (  TestTagAttribute []  )
                                type.GetCustomAttributes(typeof(TestTagAttribute), true);

                    if (attributes.Length > 0)
                    {
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

                        // type.GetConstructors(System.Reflection.BindingFlags.CreateInstance);
                        var ci = type.GetConstructor(  new Type[] {}  );
                        if (ci == null)
                        {
                            errorHandler?.Invoke(type, false);

                            continue;
                        }

                        var t = ci?.Invoke(new object[] {});
                        if (t is not null)
                        {
                            var task = (TestTask) t;

                            lock (result)
                                result.Add(task);
                        }
                    }
                }
            }
        );

        return result;
    }

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

                                                                            /// <summary>Список тегов, участвующих в условии</summary>
    public List<TestTaskTag>? listOfNeedTags;                               /// <summary>Оператор, который будет применён к тегам (and, count, tree)</summary>
    public ConditionOperator  conditionOperator;                            /// <summary>Необходимое количество повторов для операторов Count и TreeCount; при использовании этого оператора должно быть больше 0</summary>
    public Int64              countForConditionOperator;                    /// <summary>Результат вычислений подвергается логическому отрицанию</summary>
    public bool               isReversedCondition = false;                  /// <summary>Если этому правилу соответствует задача, то она вызовет срабатывание false вне зависимости от оператора</summary>
    public bool               isMandatoryExcludingRule = false;             /// <summary>Побеждает задача с большим приоритетом; только для TreePriority. Для одинакового приоритета - and</summary>
    public double             priorityForCondition = double.MinValue;

    public List<TestTaskTagCondition>? listOfNeedConditions;

    public virtual bool isSatisfiesForTask(TestTask task)
    {
        return isSatisfiesForTask_withoutReverse(task) ^ isReversedCondition;
    }

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


public abstract class TestTask
{
    public delegate void TestTaskFn();

    public TestTask(string Name)
    {
        this.Name = Name;
        this.taskFunc = () => { throw new NotImplementedException(); };

        var attributes = (  TestTagAttribute []  )
                         this.GetType().GetCustomAttributes(typeof(TestTagAttribute), true);

        foreach (var attribute in attributes)
        {
            tags.Add(attribute.tag);
            if (attribute.singleThread)
            {
                waitBefore = true;
                waitAfter  = true;
            }
        }
    }
                                                                    /// <summary>Каким тегам удовлетворяет задача</summary>
    public List<TestTaskTag> tags = new List<TestTaskTag>();

    public          TestTaskFn  taskFunc {get; protected set;}
    public          string      Name     {get; protected set;}
    public          bool        start = false;
    public          bool        ended = false;
    public readonly List<Error> error = new List<Error>();

    public DateTime started = default;
    public DateTime endTime = default;

                                                                    /// <summary>Перед выполнением задач программа ждёт завершения всех предыдущих задач</summary>
    public bool waitBefore = false;                                 /// <summary>После постановки задачи программа ждёт завершения этой задачи</summary>
    public bool waitAfter  = false;

    /// <summary>Выполнение задачи в процентах (0-100)</summary>
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


[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class TestTagAttribute: Attribute
{
    public readonly TestTaskTag tag;
    public readonly bool        singleThread = false;
    public readonly bool        notAutomatic = false;
    public TestTagAttribute(string tagName = "", double priority = 0.0, bool singleThread = false, bool notAutomatic = false)
    {
        tag = new TestTaskTag(tagName, priority);

        this.singleThread = singleThread;
        this.notAutomatic = notAutomatic;
    }
}
