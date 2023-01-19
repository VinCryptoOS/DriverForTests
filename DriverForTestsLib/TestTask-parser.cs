namespace DriverForTestsLib;
using static TestTaskTagCondition;

/// <summary>Этот класс может быть использован для того,
/// чтобы распарсить простые правила исключения тегов</summary>
public class TestConditionParser
{
    /// <summary>Результат работы парсера: условия на выполняемые TestTask</summary>
    public TestTaskTagCondition? resultCondition = null;

    /// <summary>Парсит простую строку параметров</summary>
    /// <param name="tags">Строка вида +ТегДляПриоритетногоВключения ПростоТегДляВключения -ТегДляИсключения</param>
    /// <param name="outputToConsole">true - вывести на консоль теги</param>
    /// <remarks>
    /// <para>Теги для приоритетного включения являются тегами, которые будут обязательно включены</para>
    /// <para>Теги для включения будут включены тогда, когда не исключаются тегами для исключения</para>
    /// </remarks>
    public TestConditionParser(string tags, bool outputToConsole = false)
    {
        resultCondition     = null;
        var conditions      = new TestTaskTagCondition();

        var notCondition    = new TestTaskTagCondition();
        var yesCondition    = new TestTaskTagCondition();

        conditions.conditionOperator         = ConditionOperator.TreeAnd;
        conditions.countForConditionOperator = 1;
        conditions.listOfNeedConditions      = new List<TestTaskTagCondition>
        {
            notCondition, yesCondition
        };

        notCondition.conditionOperator         = ConditionOperator.TreeCount;
        notCondition.countForConditionOperator = 1;
        yesCondition.conditionOperator         = ConditionOperator.Count;
        yesCondition.countForConditionOperator = 1;

        notCondition.isReversedCondition      = true;
        notCondition.isMandatoryExcludingRule = false;
        notCondition.listOfNeedConditions     = new List<TestTaskTagCondition>();

        yesCondition.listOfNeedTags           = new List<TestTaskTag>();

        var args = tags.Split(new string[] {" ", ","}, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

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

            if (outputToConsole)
            {
                if (not)
                    Console.WriteLine($"test without tag '{arg}'");
                else
                if (yes)
                    Console.WriteLine($"test with  mandatory  tag '{arg}'");
                else
                    Console.WriteLine($"test with tag '{arg}'");
            }

            if (not || yes)
            {
                var condition               = new TestTaskTagCondition();
                condition.conditionOperator = TestTaskTagCondition.ConditionOperator.Count;
                condition.listOfNeedTags    = new List<TestTaskTag>
                {
                    new TestTaskTag(arg, double.MinValue)
                };

                condition.countForConditionOperator = 1;
                condition.isMandatoryExcludingRule  = yes;
                notCondition.listOfNeedConditions.Add(condition);
            }

            if (!not)
            {
                var tag = new TestTaskTag(arg, double.MinValue);
                yesCondition.listOfNeedTags.Add(tag);
            }
        }

        // Если этого не сделать,
        // эта штука всегда будет выдавать false,
        // если нет никаких условий и isReversedCondition = true
        if (notCondition.listOfNeedConditions.Count <= 0)
            notCondition.conditionOperator = ConditionOperator.AlwaysFalse;
        if (yesCondition.listOfNeedTags.Count <= 0)
            yesCondition.conditionOperator = ConditionOperator.AlwaysTrue;

        resultCondition = conditions;
    }
}
