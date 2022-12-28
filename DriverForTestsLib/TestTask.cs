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
    public enum ConditionOperator { Error = 0, And = 1, Count = 2, TreeAnd = 4, TreeCount = 8 };

                                                                            /// <summary>Список тегов, участвующих в условии</summary>
    public List<TestTaskTag>? listOfNeedTags;                               /// <summary>Оператор, который будет применён к тегам (and, count, tree)</summary>
    public ConditionOperator  conditionOperator;                            /// <summary>Необходимое количество повторов для операторов Count и TreeCount; при использовании этого оператора должно быть больше 0</summary>
    public Int64              countForConditionOperator;
    public bool               isReversedCondition = false;

    public List<TestTaskTagCondition>? listOfNeedConditions;

    public bool isSatisfiesForTask(TestTask task)
    {
        return isSatisfiesForTask_withoutReverse(task) ^ isReversedCondition;
    }

    protected bool isSatisfiesForTask_withoutReverse(TestTask task)
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

            default:
                throw new Exception($"TestTaskTagCondition.isSatisfiesForTask: Illegal value of conditionOperator: {(int) conditionOperator}");
        }
    }

    protected bool isSatisfiesForTask_And(TestTask task)
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

    protected bool isSatisfiesForTask_Count(TestTask task)
    {
        if (listOfNeedTags == null)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_And: {nameof(listOfNeedTags)} == null");
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

    protected bool isSatisfiesForTask_TreeAnd(TestTask task)
    {
        if (listOfNeedConditions == null)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_And: {nameof(listOfNeedConditions)} == null");

        foreach (var condition in listOfNeedConditions)
        {
            if (!condition.isSatisfiesForTask(task))
                return false;
        }

        return true;
    }

    protected bool isSatisfiesForTask_TreeCount(TestTask task)
    {
        if (listOfNeedConditions == null)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_And: {nameof(listOfNeedTags)} == null");
        if (countForConditionOperator <= 0)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_Count: {nameof(countForConditionOperator)} <= 0");

        var cnt = 0;
        foreach (var condition in listOfNeedConditions)
        {
            if (condition.isSatisfiesForTask(task))
                cnt++;
        }

        return cnt >= countForConditionOperator;
    }
}


public class TestTask
{
    public delegate void TestTaskFn();

    public TestTask(string Name, TestTaskFn task)
    {
        this.Name = Name;
        this.task = task;
    }
                                                                    /// <summary>Каким тегам удовлетворяет задача</summary>
    public List<TestTaskTag> tags = new List<TestTaskTag>();

    public readonly TestTaskFn  task;
    public readonly string      Name;
    public          bool        start = false;
    public          bool        ended = false;
    public readonly List<Error> error = new List<Error>();

    public DateTime started = default;
    public DateTime endTime = default;

    public bool waitBefore = false;
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
