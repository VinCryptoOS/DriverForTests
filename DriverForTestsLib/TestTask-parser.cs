namespace DriverForTestsLib;
using static TestTaskTagCondition;

/// <summary>Этот класс может быть использован для того,
/// чтобы распарсить простые правила исключения тегов</summary>
public class TestConditionParser
{
    /// <summary>Результат работы парсера: условия на выполняемые TestTask</summary>
    public TestTaskTagCondition? resultCondition = null;

    /// <summary>Парсит простую строку параметров</summary>
    /// <param name="tags">Строка вида +ТегДляПриоритетногоВключения ПростоТегДляВключения -ТегДляИсключения &lt;2 ЕщёОдинТегБудетВключатсяЕслиDurationНеБолее2</param>
    /// <param name="outputToConsole">true - вывести на консоль теги</param>
    /// <remarks>
    /// <para>Теги для приоритетного включения являются тегами, которые будут обязательно включены</para>
    /// <para>Теги для включения будут включены тогда, когда не исключаются тегами для исключения</para>
    /// </remarks>
    public TestConditionParser(string tags, bool outputToConsole = false)
    {
        resultCondition = null;
        var conditions  = new TestTaskTagCondition();

        var args = tags.Split(new string[] { " ", "," }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var positive  = new SortedList<int, List<TestTagDescription>>(8);
        var mandatory = new SortedList<int, List<TestTagDescription>>(8);
        var durations = new SortedList<int, double>(8);

        double duration = -1d;
        int    dIndex   = 0;
        addDurationForTestTagDescriptionList(dIndex, duration);

        // Каждый аргумент - имя тега
        foreach (var _arg in args)
        {
            var arg = _arg;
            var not = false;
            var yes = false;
            // Убираем "-", говорящий, что данный тег нужно исключить
            if (arg.StartsWith("-"))
            {
                arg = arg.Substring(1);
                not = true;
            }
            else
            if (arg.StartsWith("+"))
            {
                arg = arg.Substring(1);
                yes = true;
            }
            else
            if (arg.StartsWith("<"))
            {
                arg = arg.Substring(1);
                duration = double.Parse(arg);
                addDurationForTestTagDescriptionList(++dIndex, duration);

                if (outputToConsole)
                    Console.WriteLine($"test with duration <= '{duration}'");

                continue;
            }
            else
            if (arg == "?")
                arg = null;

            if (outputToConsole)
            {
                if (not)
                    Console.WriteLine($"test without tag '{arg}'");
                else
                if (yes)
                    Console.WriteLine($"test with  mandatory  tag '{arg}'");
                else
                    Console.WriteLine($"test with tag '{arg ?? "<all tags>"}'");
            }

            // Если задача обязательная (mandatory)
            if (yes)
                AddNewTestTagDescriptionToList(mandatory, dIndex, duration, true, arg);
            else
                AddNewTestTagDescriptionToList(positive,  dIndex, duration, !not, arg);
        }

        conditions.conditionOperator = ConditionOperator.TreePriority;
        conditions.countForConditionOperator = 1;
        conditions.listOfNeedConditions = new List<TestTaskTagCondition>(8);

        var mandatoryConditions = new TestTaskTagCondition()
                                        {
                                            priorityForCondition = double.MaxValue,
                                            listOfNeedConditions = new List<TestTaskTagCondition>(),
                                            conditionOperator    = ConditionOperator.TreeCount
                                        };
        var aConditions         = new TestTaskTagCondition()
                                        {
                                            priorityForCondition = 0,
                                            listOfNeedConditions = new List<TestTaskTagCondition>(),
                                            conditionOperator    = ConditionOperator.TreeCount
                                        };

        conditions.listOfNeedConditions.Add(mandatoryConditions);
        conditions.listOfNeedConditions.Add(aConditions);

        // Сначала добавляем mandatory-задачи
        // Их добавление происходит без приоритета,
        // т.к. они никогда не ничем не переопределяются и приоритет не имеет значения
        for (int p = 0; p < durations.Count; p++)
        {
            var cnd = new TestTaskTagCondition()
            {
                listOfNeedTags    = new List<TestTaskTag>(),
                conditionOperator = ConditionOperator.Count
            };

            mandatoryConditions.listOfNeedConditions.Add(cnd);

            foreach (var ts in mandatory[p])
            {
                var newTag = new TestTaskTag(ts.Name, double.MinValue, ts.Duration);
                cnd.listOfNeedTags.Add(newTag);
            }
        }

        for (int p = 0; p < durations.Count; p++)
        {
            var cnd = new TestTaskTagCondition()
            {
                priorityForCondition = p,
                listOfNeedConditions = new List<TestTaskTagCondition>(positive[p].Count),
                conditionOperator    = ConditionOperator.TreeCount
            };

            aConditions.listOfNeedConditions.Add(cnd);

            foreach (var ts in positive[p])
            {
                var newCond = new TestTaskTagCondition()
                {
                    listOfNeedTags           = new List<TestTaskTag>(1),
                    isMandatoryExcludingRule = !ts.isPositive,
                    conditionOperator        = ConditionOperator.Count
                };
                cnd.listOfNeedConditions.Add(newCond);

                var newTag = new TestTaskTag(ts.Name, double.MinValue, ts.Duration)    {    maxDuration = !ts.isPositive    };
                newCond.listOfNeedTags.Add(newTag);
            }
        }

        resultCondition = conditions;

        void addDurationForTestTagDescriptionList(int index, double duration)
        {
            durations.Add(index, duration);
            positive .Add(index, new List<TestTagDescription>(8));
            mandatory.Add(index, new List<TestTagDescription>(8));
        }

        static void AddNewTestTagDescriptionToList(SortedList<int, List<TestTagDescription>> list, int dIndex, double duration, bool isPositive, string? arg)
        {
            var ttd = new TestTagDescription { Name = arg, Duration = duration, isPositive = isPositive };
            list[dIndex].Add(ttd);
        }
    }

    public class TestTagDescription
    {
        public string? Name         {get; init;}
        public double  Duration     {get; init;}
        public bool    isPositive   {get; init;}
    }
}
