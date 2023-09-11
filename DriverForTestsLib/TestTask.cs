using System.Collections.Concurrent;
using System.Diagnostics;

namespace DriverForTestsLib;

using static IsSatisfies;

/// <summary>Класс описывает ошибку, возникшую в ходе теста</summary>
public class TestError
{
    public Exception? ex = null;
    public string     Message = "";
}

public class IsSatisfies
{
    /// <summary>Прдеставляет тип-перечисление возможных значений объекта IsSatisfies</summary>
    public enum Signs { no = -1, unknown = 0, yes = 1 }

    /// <summary>Перечисляет все возможные значения (no, unknown, yes)</summary>
    public static IEnumerable<IsSatisfies> enumAllPossibleValues()
    {
        yield return NO;
        yield return UNK;
        yield return YES;
    }

    public class FreezedException: Exception
    {}

    protected Signs _val;
    public    Signs  val
    {
        get => _val;
        set
        {
            if (_isFreezed)
                throw new FreezedException();

            _val = value;
        }
    }

    protected bool _isFreezed = false;
    public    bool  isFreezed
    {
        get => _isFreezed;
        set
        {
            if (value)
                _isFreezed = true;
        }
    }

    /// <summary>Создаёт объект</summary>
    /// <param name="value">Значение объекта</param>
    /// <param name="freezed">Объект заморожен (нет возможности изменять значение объекта)</param>
    public IsSatisfies(Signs value, bool freezed = false)
    {
        this.val       = value;
        this.isFreezed = freezed;
    }

    /// <summary>Создаёт объект из булевской переменной: true == yes, false = no</summary>
    /// <param name="bValue">Булевская переменная</param>
    public IsSatisfies(bool bValue): this(bValue ? Signs.yes : Signs.no)
    {}

    /// <summary>Осуществляет операцию отрицания (обращает логическую переменную @is)</summary>
    /// <param name="is">Переменная для обращения</param>
    /// <returns>Результат обращения</returns>
    public Signs doReverse(Signs @is)
    {
        switch (@is)
        {
            case Signs.yes:     return Signs.no;
            case Signs.no :     return Signs.yes;
            case Signs.unknown: return Signs.unknown;

            default:
                throw new NotImplementedException($"IsSatisfies_helper.doReverse (for {@is})");
        }
    }

    /// <summary>Осуществляет операцию отрицания над значением val (обращает её с помощью doReverse(Signs @is))</summary>
    public IsSatisfies doReverse()
    {
        var a = this.LightClone();
        a.val = a.doReverse(a.val);

        return a;
    }

    /// <summary>Преобразует логическую переменную @is в булеву перменную</summary>
    /// <param name="is">Логическая переменная для преобразования</param>
    /// <param name="unknown">Значение по умолчанию, если @is не определена</param>
    /// <returns></returns>
    public bool toBool(Signs @is, bool unknown = true)
    {
        switch (@is)
        {
            case Signs.yes:     return true;
            case Signs.no :     return false;
            case Signs.unknown: return unknown;

            default:
                throw new NotImplementedException($"IsSatisfies.toBool (for {@is})");
        }
    }

    /// <summary>Преобразует значение объекта (поле val) в булеву перменную и возвращает полученный результат (val не изменяется)</summary>
    /// <param name="unknown">Значение по умолчанию, если @is не определена</param>
    /// <returns>Вычисленное значение</returns>
    public bool toBool(bool unknown = true) => toBool(this.val, unknown);
                                                                                                /// <summary>Возвращает true, если значение объекта равно yes</summary>
    public bool yes => val == Signs.yes;                                                        /// <summary>Возвращает true, если значение объекта равно no</summary>
    public bool no  => val == Signs.no;                                                         /// <summary>Возвращает true, если значение объекта равно unknown</summary>
    public bool unk => val == Signs.unknown;

    public static bool operator ==(IsSatisfies a, IsSatisfies b)
    {
        return a.val == b.val;
    }

    public static bool operator !=(IsSatisfies a, IsSatisfies b)
    {
        return a.val != b.val;
    }

    public override bool Equals(object? obj)
    {
        if (obj is IsSatisfies v)
            return this == v;

        return false;
    }

    public override int GetHashCode() => (int) val ^ -0x075F_ECAA;
    public override string ToString() => val.ToString();

                                                                                                /// <summary>Статический объект, представляющий значение yes</summary>
    public static IsSatisfies YES = new IsSatisfies(Signs.yes, true);                           /// <summary>Статический объект, представляёщий значение no</summary>
    public static IsSatisfies NO  = new IsSatisfies(Signs.no, true);                            /// <summary>Статический объект, представляёщий значение unknown</summary>
    public static IsSatisfies UNK = new IsSatisfies(Signs.unknown);
                                                                                                /// <summary>Копирует значение val в новый незамороженный объект</summary>
    public IsSatisfies LightClone() => new IsSatisfies(this.val);
}

/// <summary>Этот класс должен быть переопределён потомком.
/// Он создаёт список нефильтрованных задач и определяет условия их фильтрации</summary>
public abstract class TestConstructor
{                                                                               /// <summary>Условие на выполнение задач</summary>
    public TestTaskTagCondition? conditions;                                    /// <summary>Общий приоритет на выполнение.<para>Если у задачи нет хотя бы одного тега с приоритетом не менее generalPriorityForTasks, то она будет пропущена. Задачи без тегов выполняются</para></summary>
    public double                generalPriorityForTasks = double.MinValue;     /// <summary>Общий параметр длительности на выполнение.<para>Если у задачи есть хотя бы один тег с приоритетом более generalDuration, то она будет пропущена. Задачи без тегов выполняются. Тег с параметром менее 0 не учитывается</para></summary>
    public double                generalDuration         = -1d;

    /// <summary>Метод, заполняющий нефильтрованный список задач для выполнения в тестах</summary>
    /// <param name="tasks">Список для заполнения задачами</param>
    public abstract void CreateTasksLists(ConcurrentQueue<TestTask> tasks);

    /// <summary>Определяет, нужно ли запускать эту задачу в зависимости от тегов и this.generalPriorityForTasks</summary>
    /// <returns>true - задачу нужно запускать</returns>
    public virtual bool ShouldBeExecuted(TestTask task)
    {
        if (task.isSatisfiesThePriorityAndDuration(generalPriorityForTasks, generalDuration).no)
            return false;

        if (conditions == null)
            return true;

        return conditions.isSatisfiesForTask(task).toBool();
    }

    /// <summary>Этот метод используется для сообщения в вызывающую программу о том,
    /// что при автоматической постановке задач выявлена невозможность её постановки</summary>
    /// <param name="TaskType">Тип тестовой задачи, которую метод пытался поставить</param>
    /// <param name="notAutomatic">Если true, то в одном из атрибутов задачи установлен флаг notAutomatic, то есть она штатно не должна добавляться автоматически</param>
    public delegate void ErrorTaskHandler(Type TaskType, bool notAutomatic);

    /// <summary>Это статический метод, который получает тестовые задачи из всех загруженных сборок</summary>
    /// <param name="errorHandler">Обработчик задач, которые автоматически не могут быть получены</param>
    /// <returns>Возвращает список задач, которые можно вручную добавить в список на выполнение (смотреть addTasksForQueue)</returns>
    public List<TestTask> getTasksFromAppDomain(ErrorTaskHandler? errorHandler)
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
                    var ci = type.GetConstructor(  new Type[] {typeof(TestConstructor)}  );
                    if (ci == null)
                    {
                        errorHandler?.Invoke(type, false);

                        continue;
                    }

                    // Создаём экземпляр тестовой задачи конструктором по умолчанию
                    var t = ci?.Invoke(new object[] {this});
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
                                                                        /// <summary>Имя тега. Должно быть всегда не null для конкретной задачи. Если null, то это значит, что это тег фильтра: если с таким тегом сравнивается задача, то он будет удовлетворять любому другому тегу</summary>
    public readonly string? name;                                       /// <summary>Приоритет тега: чем больше, тем выше приоритет</summary>
    public readonly double  priority = 0.0d;                            /// <summary>Условная длительность теста (параметр длительности). В конструкторе TestTask на каждый тег устанавливается максимальная длительность, вычисленная со всех тегов</summary>
    public          double  duration = -1d;                             /// <summary>Для аттрибутов задач данный тег не имеет смысла. true - нормальное значение тега. Указывает на то, что duration - это максимальная продолжительность (false используется только в тегах для фильтрации и указывает на то, что задача должна быть строго более duration)</summary>
    public          bool    maxDuration = true;

    /// <param name="tagName">Имя тега</param>
    /// <param name="tagPriority">Приоритет тега</param>
    /// <param name="tagDuration">Параметр длительности задачи</param>
    public TestTaskTag(string? tagName, double tagPriority, double tagDuration)
    {
        name     = tagName;
        priority = tagPriority;
        duration = tagDuration;
        // DebugName = "TestTaskTag.DEBUG.Name." + Interlocked.Increment(ref CountOfObjects);
    }
/*
    public static int    CountOfObjects;
    public        string DebugName;*/
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
    public List<TestTaskTag>? listOfNeedTags;                                   /// <summary>Оператор, который будет применён к тегам (and, count, tree)</summary>
    public ConditionOperator  conditionOperator;                                /// <summary>Необходимое количество повторов для операторов Count и TreeCount; при использовании этого оператора должно быть больше 0</summary>
    public Int64              countForConditionOperator = 1;                    /// <summary>Результат вычислений подвергается логическому отрицанию</summary>
    public bool               isReversedCondition = false;                      /// <summary>Если этому правилу соответствует задача, то она вызовет срабатывание false на всём условии вне зависимости от оператора</summary>
    public bool               isMandatoryExcludingRule = false;                 /// <summary>Побеждает задача с большим приоритетом (если она yes, то условие выполнено); только для TreePriority. Для одинакового приоритета - and</summary>
    public double             priorityForCondition = double.MinValue;

    /// <summary>Список подусловий, участвующих в этом условии. Используется с операторами TreeAnd, TreeCount, TreePriority</summary>
    public List<TestTaskTagCondition>? listOfNeedConditions;

    /// <summary>Проверяет, удовлетворяет ли задача task этому условию</summary>
    /// <param name="task">Проверяемая задача</param><returns>true, если задача удовлетворяет этому условию</returns>
    public virtual IsSatisfies isSatisfiesForTask(TestTask task)
    {
        var v = isSatisfiesForTask_withoutReverse(task);
        if (isReversedCondition)
            v = v.doReverse();

        return v;
    }

    /// <summary>Проверка, аналогичная isSatisfiesForTask, но без учёта isReversedCondition</summary>
    /// <param name="task">Проверяемая задача</param><returns>true, если задача удовлетворяет этому условию без учёта isReversedCondition</returns>
    protected virtual IsSatisfies isSatisfiesForTask_withoutReverse(TestTask task)
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
                return new IsSatisfies(Signs.yes);

            case ConditionOperator.AlwaysFalse:
                return new IsSatisfies(Signs.no);

            case ConditionOperator.TreePriority:
                return isSatisfiesForTask_TreePriority(task);

            default:
                throw new Exception($"TestTaskTagCondition.isSatisfiesForTask: Illegal value of conditionOperator: {(int) conditionOperator}");
        }
    }

    /// <summary>Проверка оператора And</summary>
    /// <param name="task">Задача для проверки</param><returns>true, если задача удовлетворяет этому условию без учёта isReversedCondition</returns>
    protected virtual IsSatisfies isSatisfiesForTask_And(TestTask task)
    {
        if (listOfNeedTags == null)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_And: {nameof(listOfNeedTags)} == null");

        IsSatisfies iss = IsSatisfies.UNK;
        foreach (var tag in listOfNeedTags)
        {
            var v = task.isSatisfiesTag(tag);
            if (v.yes)
            {
                if (iss.unk)
                    iss = IsSatisfies.YES;
            }
            else
            if (v.no)
            {
                return IsSatisfies.NO;
            }
        }

        return iss;
    }

    /// <summary>Проверка оператора Count</summary>
    /// <param name="task">Задача для проверки</param><returns>true, если задача удовлетворяет этому условию без учёта isReversedCondition</returns>
    protected virtual IsSatisfies isSatisfiesForTask_Count(TestTask task)
    {
        if (listOfNeedTags == null)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_Count: {nameof(listOfNeedTags)} == null");
        if (countForConditionOperator <= 0)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_Count: {nameof(countForConditionOperator)} <= 0");

        if (listOfNeedTags.Count < countForConditionOperator)
            return IsSatisfies.UNK;

        var ycnt = 0;
        var acnt = 0;
        foreach (var tag in listOfNeedTags)
        {
            var tr = task.isSatisfiesTag(tag);

            if (tr.yes)
                ycnt++;

            if (!tr.unk)
                acnt++;
        }

        if (acnt < countForConditionOperator)
            return IsSatisfies.UNK;

        return ycnt >= countForConditionOperator ? IsSatisfies.YES : IsSatisfies.NO;
    }

    /// <summary>Проверка оператора TreeAnd</summary>
    /// <param name="task">Задача для проверки</param><returns>true, если задача удовлетворяет этому условию без учёта isReversedCondition</returns>
    protected virtual IsSatisfies isSatisfiesForTask_TreeAnd(TestTask task)
    {
        if (listOfNeedConditions == null)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_And: {nameof(listOfNeedConditions)} == null");

        if (listOfNeedConditions.Count <= 0)
            return IsSatisfies.UNK;

        var result = IsSatisfies.UNK;
        foreach (var condition in listOfNeedConditions)
        {
            if (condition.isMandatoryExcludingRule)
            {
                if (condition.isSatisfiesForTask(task).yes)
                    return IsSatisfies.NO;

                continue;
            }

            var r = condition.isSatisfiesForTask(task);
            if (r.no)
                return IsSatisfies.NO;
            else
            if (r.yes && result.unk)
                result = IsSatisfies.YES;
        }

        return result;
    }

    /// <summary>Проверка оператора TreeCount</summary>
    /// <param name="task">Задача для проверки</param><returns>true, если задача удовлетворяет этому условию без учёта isReversedCondition</returns>
    protected virtual IsSatisfies isSatisfiesForTask_TreeCount(TestTask task)
    {
        if (listOfNeedConditions == null)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_Count: {nameof(listOfNeedTags)} == null");
        if (countForConditionOperator <= 0)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_Count: {nameof(countForConditionOperator)} <= 0");
        
        if (listOfNeedConditions.Count < countForConditionOperator)
            return IsSatisfies.UNK;

        var ycnt = 0;
        var acnt = 0;
        foreach (var condition in listOfNeedConditions)
        {
            if (condition.isMandatoryExcludingRule)
            {
                if (condition.isSatisfiesForTask(task).yes)
                    return IsSatisfies.NO;

                continue;
            }

            var r = condition.isSatisfiesForTask(task);
            if (r.yes)
                ycnt++;

            // Мы подсчитываем только сработавшие задачи. Если их будет недостаточно, то считаем, что само условие просто к этой задаче вообще не применимо
            if (!r.unk)
                acnt++;
        }

        // Если встретились только isMandatoryExcludingRule-задачи или, в целом, обычных задач было недостаточно, чтобы набрать нужное число, даже если все они удовлетворят условию
        if (acnt < countForConditionOperator)
            return IsSatisfies.UNK;

        return ycnt >= countForConditionOperator ? IsSatisfies.YES : IsSatisfies.NO;
    }

    /// <summary>Проверка оператора TreePriority: побеждает задача с большим приоритетом (если yes, то yes, если no, то no). Если задач с одним и тем же приоритетом более одной, ни одна не должна возвратить IsSatisfies.NO</summary>
    /// <param name="task">Задача для проверки</param><returns>true, если задача удовлетворяет этому условию без учёта isReversedCondition</returns>
    protected virtual IsSatisfies isSatisfiesForTask_TreePriority(TestTask task)
    {
        if (listOfNeedConditions == null)
            throw new Exception($"TestTaskTagCondition.isSatisfiesForTask_TreePriority: {nameof(listOfNeedTags)} == null");

        var result = IsSatisfies.UNK;
        var curP   = double.MinValue;
        foreach (var condition in listOfNeedConditions)
        {
            if (condition.isMandatoryExcludingRule)
            {
                if (condition.isSatisfiesForTask(task).yes)
                    return IsSatisfies.NO;

                continue;
            }

            if (condition.priorityForCondition > curP)
            {
                var tr = condition.isSatisfiesForTask(task);
                if (!tr.unk)
                {
                    result = tr;
                    curP   = condition.priorityForCondition;
                }
            }
            else
            if (condition.priorityForCondition == curP)
            {
                if (condition.isSatisfiesForTask(task).no)
                    result = IsSatisfies.NO;
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

    /// <summary>Конструктор этой тестовой задачи для вызова метода ShouldBeExecuted</summary>
    public TestConstructor? constructor {get; protected set;}

    /// <summary>Определяет, нужно ли выполнять данную задачу исходя из фильтров</summary>
    /// <returns>Возвращает true, если задача должна быть выполнена в данной серии тестов</returns>
    public bool ShouldBeExecuted()
    {
        if (constructor == null)
            return true;

        return constructor.ShouldBeExecuted(this);
    }

    /// <param name="Name">Имя задачи: может быть не уникальным, однако, для идентификации задач в логе рекомендуется уникальное имя</param>
    /// <param name="constructor">Конструктор, который создаёт эту задачу</param>
    public TestTask(string Name, TestConstructor? constructor)
    {
        this.Name        = Name;
        this.constructor = constructor;
        this.taskFunc    = () => { throw new NotImplementedException(); };
        this.doneFunc    = () => {};

        var attributes   = (  TestTagAttribute []  )
                           this.GetType().GetCustomAttributes(typeof(TestTagAttribute), true);

        var maxTagsDuration = double.MinValue;
        // Применение атрибутов к задаче
        foreach (var attribute in attributes)
        {
            // Добавляем теги из атрибутов
            if (attribute.tag is not null)
            {
                if (attribute.tag.name == null)
                    throw new ArgumentNullException("TestTaskTag can not null (null for filter patterns only)");

                tags.Add(attribute.tag);
                if (maxTagsDuration < attribute.tag.duration)
                    maxTagsDuration = attribute.tag.duration;
            }

            // Устанавливаем флаг экслюзивной задачи, который выделен весь процессор целиком
            if (attribute.singleThread)
            {
                waitBefore = true;
                waitAfter  = true;
            }
        }

        // Устанавливаем максимальную длительность, чтобы не нужно было в атрибутах дублировать эти длительности
        foreach (var tag in tags)
        {
            tag.duration = maxTagsDuration;
        }
    }
                                                                    /// <summary>Каким тегам удовлетворяет задача</summary>
    public List<TestTaskTag> tags = new List<TestTaskTag>();

                                                                    /// <summary>Функция тестирования, которая вызывается библиотекой</summary>
    public virtual TestTaskFn   taskFunc {get; protected set;}      /// <summary>Имя задачи</summary>
    public virtual  string      Name     {get;           set;}      /// <summary>Если true, то задача стартовала (остаётся true навсегда)</summary>
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
    /// <summary>Когда необходимо обновить поле done, вызывается эта функция</summary>
    public virtual TestTaskFn? doneFunc {get; protected set;}

    /// <summary>Проверяет, удовлетворяет ли задача указанному приоритету и параметру длительности</summary>
    /// <param name="generalPriorityForTasks">Заданный приоритет</param>
    /// <param name="maxDuration">Заданный параметр времени выполнения (-1d - нет требований)</param>
    /// <returns>true, если нет тегов вообще и или есть хоть один тег с приоритетом не менее generalPriorityForTasks</returns>
    public virtual IsSatisfies isSatisfiesThePriorityAndDuration(double generalPriorityForTasks, double maxDuration)
    {
        if (tags.Count <= 0)
            return IsSatisfies.UNK;

        bool isSatisfiesThePriority = false;
        foreach (var tag in tags)
        {
            if (tag.priority >= generalPriorityForTasks)
            {
                isSatisfiesThePriority = true;
                break;
            }
        }
        if (!isSatisfiesThePriority)
            return IsSatisfies.NO;

        // Если maxDuration - значит, требований не предъявляется
        if (maxDuration < 0)
            return IsSatisfies.YES;

        foreach (var tag in tags)
        {
            if (tag.duration > maxDuration)
            {
                return IsSatisfies.NO;
            }
        }

        return IsSatisfies.YES;
    }

    /// <summary>Определяет, удовлетворяет ли задача заданному тегу с учётом указанного приоритета</summary>
    /// <param name="tag">Заданный тег, которому должна удовлетворять задача. Если тегов с таким именем нет - не удовлетворяет</param>
    /// <returns>"yes", если задача удовлетворяет тегу</returns>
    public virtual IsSatisfies isSatisfiesTag(TestTaskTag tag)
    {
        IsSatisfies isSatisfies = IsSatisfies.UNK;
        IsSatisfies durFlag     = tag.duration < 0 ? IsSatisfies.YES : IsSatisfies.UNK;

        // Если тегов нет вообще, то мы не удовлетворяем ничему
        if (tags.Count <= 0)
        {
            if (tag.name == null)           // Тег <all tags> всегда всему удовлетворяет
                return IsSatisfies.YES;
            else
                return IsSatisfies.NO;
        }

        foreach (var t in tags)
        {
            if (tag.name != null)           // null удовлетворяет любому поисковому условию
            {
                if (t.name != tag.name)
                    continue;
            }

            // Если maxDuration сброшен, значит мы ищем длительные задачи и приоритет не важен, т.к. все эти задачи идут в инвертированных правилах
            if (t.priority >= tag.priority || !tag.maxDuration)
            {
                if (!isSatisfies.yes)
                    isSatisfies = IsSatisfies.YES;
            }

            // Даже если приоритет неверный, всё равно проверяем задачу на длительность
            if (t.duration >= 0 && tag.duration >= 0)
            {
                if (durFlag.unk)
                    durFlag = IsSatisfies.NO;

                if (t.duration > tag.duration)      // Выполняем все задачи, которые занимают не более tag.duration. Неравенство строгое
                {
                    if (tag.maxDuration)
                        return IsSatisfies.NO;
                    else
                        durFlag = IsSatisfies.YES;
                }
            }
        }

        if (tag.maxDuration)
        {
            if (isSatisfies.unk)
                return IsSatisfies.NO;

            return isSatisfies;
        }

        if (isSatisfies.unk)
            return IsSatisfies.NO;

        return durFlag;
    }
}

/// <summary>Класс определяет атрибут, который навешивается на наследника TestTask.
/// На тестовую задачу вешается тег с соответствующим именем</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class TestTagAttribute: Attribute
{                                                                           /// <summary>Тег, установленный атрибутом (может быть null)</summary>
    public readonly TestTaskTag? tag;                                       /// <summary>Если true, то тестовая задача выполняется одна на всём процессоре (другие тестовые задачи не выполняются в это время)</summary>
    public readonly bool         singleThread = false;                      /// <summary>Если true, то задача не будет автоматически регистрироваться на выполнение функцией TestConstructor.getTasksFromAppDomain (нужно добавить её вручную)</summary>
    public readonly bool         notAutomatic = false;                      /// <summary>Предполагаемый параметр времени выполнения для фильтрации медленных тестов. Отрицательное значение - игнорируется</summary>
    public readonly double       duration     = -1d;

    /// <param name="tagName">Имя тега (может быть null)</param>
    /// <param name="priority">Приоритет тега</param>
    /// <param name="singleThread">Задача для эксплюзивного выполнения на всём процессоре (другие тестовые задачи не будут выполняться одновременно)</param>
    /// <param name="notAutomatic">Задача не будет автоматически поставлена на выполнение (требуется ручная регистрация)</param>
    /// <param name="duration">Задачи с большим duration, чем в фильтре задач, не будут выполнены</param>
    public TestTagAttribute(string? tagName = null, double priority = 0d, bool singleThread = false, bool notAutomatic = false, double duration = -1d)
    {
        if (tagName is not null)
            tag = new TestTaskTag(tagName, priority, duration);

        this.singleThread = singleThread;
        this.notAutomatic = notAutomatic;
        this.duration     = duration;
    }
}
