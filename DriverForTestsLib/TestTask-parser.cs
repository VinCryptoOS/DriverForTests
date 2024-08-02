namespace DriverForTestsLib;
using static TestTaskTagCondition;

/// <summary>Этот класс может быть использован для того,
/// чтобы распарсить простые правила исключения тегов</summary>
public class TestConditionParser
{
    /// <summary>Результат работы парсера: условия на выполняемые TestTask</summary>
    public TestTaskTagCondition? resultCondition = null;

    /// <summary>Парсит простую строку параметров. Документация по языку фильтрации в TestTask-parser.md</summary>
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

            // Убираем "-", говорящий, что данный тег нужно исключить, и другие предикаты
            if (arg.StartsWith("-") || arg == "-?")
            {
                if (arg == "-?")
                    arg = null;
                else
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
            // else // Если все теги только mandatory, то positive будет пусто, что приведёт к тому, что фильтр будет срабатывать всегда
                AddNewTestTagDescriptionToList(positive,  dIndex, duration, !not, arg);
        }

        conditions.conditionOperator    = ConditionOperator.TreePriority;
        conditions.listOfNeedConditions = new List<TestTaskTagCondition>(8);

        // Никогда не возвращает "no". Обрабатывает теги, указанные с "+" и всегда выводит их на обработку
        var mandatoryConditions = new TestTaskTagCondition()
                                        {
                                            priorityForCondition = double.MaxValue,
                                            listOfNeedConditions = new List<TestTaskTagCondition>(),
                                            conditionOperator    = ConditionOperator.TreeCount,
                                            isReversedCondition  = true
                                        };

        // Если mandatoryConditions не вернули "yes", то очередь за этим условием
        // Оно проверяет правила включения aConditionsY. Но если правила исключения aConditionsN говорят "no", то задача снимается
        var sConditions         = new TestTaskTagCondition()
                                        {
                                            priorityForCondition = 0,
                                            listOfNeedConditions = new List<TestTaskTagCondition>(),
                                            conditionOperator    = ConditionOperator.TreeCount
                                        };
        var aConditionsY        = new TestTaskTagCondition()
                                        {
                                            priorityForCondition = 0,
                                            listOfNeedConditions = new List<TestTaskTagCondition>(),
                                            conditionOperator    = ConditionOperator.TreeCount
                                        };
        var aConditionsN        = new TestTaskTagCondition()            // Никогда не возвращает "yes", но за счёт isReversedCondition никогда не возвращает "no"
                                        {
                                            priorityForCondition     = 1,
                                            listOfNeedConditions     = new List<TestTaskTagCondition>(),
                                            conditionOperator        = ConditionOperator.TreeCount,
                                            isMandatoryExcludingRule = true,
                                            isReversedCondition      = true
                                        };

         conditions.listOfNeedConditions.Add(mandatoryConditions);
         conditions.listOfNeedConditions.Add(sConditions);
        sConditions.listOfNeedConditions.Add(aConditionsY);
        sConditions.listOfNeedConditions.Add(aConditionsN);

        // Сначала добавляем mandatory-задачи
        // Их добавление происходит без приоритета,
        // т.к. они никогда не ничем не переопределяются и приоритет не имеет значения
        for (int p = 0; p < durations.Count; p++)
        {
            var cnd = new TestTaskTagCondition()
            {
                listOfNeedTags    = new List<TestTaskTag>(),
                conditionOperator = ConditionOperator.Count,
                isMandatoryExcludingRule = true
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

            aConditionsY.listOfNeedConditions.Add(cnd);

            // Здесь никогда не возвращается "yes", т.к. всё идёт на исключениях, а не на логическом "или"
            // За счёт isReversedCondition здесь будет возвращаться "yes", но никогда "no"
            cnd = new TestTaskTagCondition()
            {
                priorityForCondition = p,
                listOfNeedConditions = new List<TestTaskTagCondition>(positive[p].Count),
                conditionOperator    = ConditionOperator.TreeCount,
                isMandatoryExcludingRule = true,
                isReversedCondition      = true
            };

            aConditionsN.listOfNeedConditions.Add(cnd);
        }

        // Добавляем во все уровни приоритетов позитивные условия
        // 4 <0 4 <0 4 - чем правее тег, тем он более приоритетный. Значение duration не важно
        // Негативные условия нужно добавить так, чтобы они выбивали позитивные по всем уровням приоритетов
        // Например, условие -4 <0 5 должно выбивать и условие "5", то есть попасть на более высокий приоритет
        for (int p = 0; p < durations.Count; p++)
        {
            foreach (var ts in positive[p])
            {
                var aConditions = aConditionsN;
                if (ts.IsPositive)
                    aConditions = aConditionsY;

                TestTaskTagCondition newCond = newTestTaskTagCondition(ts);

                var cnd = findConditionByPriority(aConditions.listOfNeedConditions, p); if (cnd.listOfNeedConditions is null) throw new Exception();
                cnd.listOfNeedConditions.Add(newCond);
            }
        }

        resultCondition = conditions;


        static TestTaskTagCondition newTestTaskTagCondition(TestTagDescription ts)
        {
            var newCond = new TestTaskTagCondition()
            {
                listOfNeedTags = new List<TestTaskTag>(1),
                isMandatoryExcludingRule = !ts.IsPositive,
                conditionOperator = ConditionOperator.Count
            };

            // maxDuration = !ts.isPositive - если идёт исключение каких-то задач, то оно идёт для задач большего времени, а не меньшего
            var newTag = new TestTaskTag(ts.Name, double.MinValue, ts.Duration) { maxDuration = ts.IsPositive };
            newCond.listOfNeedTags.Add(newTag);
            return newCond;
        }

        void addDurationForTestTagDescriptionList(int index, double duration)
        {
            durations.Add(index, duration);
            positive .Add(index, new List<TestTagDescription>(8));
            mandatory.Add(index, new List<TestTagDescription>(8));
        }

        static void AddNewTestTagDescriptionToList(SortedList<int, List<TestTagDescription>> list, int dIndex, double duration, bool isPositive, string? arg)
        {
            var ttd = new TestTagDescription { Name = arg, Duration = duration, IsPositive = isPositive };
            list[dIndex].Add(ttd);
        }

        static TestTaskTagCondition findConditionByPriority(List<TestTaskTagCondition> list, int p)
        {
            foreach (var cnd in list)
                if (cnd.priorityForCondition == p)
                    return cnd;

            throw new Exception("TestConditionParser.findConditionByPriority: fatal algorithmic error");
        }
    }

    public class TestTagDescription
    {
        public string? Name         {get; init;}
        public double  Duration     {get; init;}
        public bool    IsPositive   {get; init;}
    }
}
