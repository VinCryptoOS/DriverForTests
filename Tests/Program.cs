using DriverForTestsLib;

namespace Tests;
using static TestTaskTagCondition;

class Program
{
    static void Main(string[] args)
    {
        var driver = new DriverForTests();
        Console.WriteLine($"args: " + args.Length);
        switch (args.Length)
        {
            case 0:
                driver.ExecuteTests( new Example1TestConstructor() );
                break;

            default:
                var testConstructor = new Example1TestConstructor();
                var conditions      = new TestTaskTagCondition();

                var notCondition    = new TestTaskTagCondition();
                var yesCondition    = new TestTaskTagCondition();

                conditions.conditionOperator         = ConditionOperator.TreeAnd;
                conditions.countForConditionOperator = 1;
                conditions.listOfNeedConditions      = new List<TestTaskTagCondition>
                {
                    notCondition, yesCondition
                };
                testConstructor.conditions = conditions;

                notCondition.conditionOperator         = ConditionOperator.TreeCount;
                notCondition.countForConditionOperator = 1;
                yesCondition.conditionOperator         = ConditionOperator.Count;
                yesCondition.countForConditionOperator = 1;

                notCondition.isReversedCondition      = true;
                notCondition.isMandatoryExcludingRule = false;
                notCondition.listOfNeedConditions     = new List<TestTaskTagCondition>();

                yesCondition.listOfNeedTags           = new List<TestTaskTag>();

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

                    if (not)
                        Console.WriteLine($"test without tag '{arg}'");
                    else
                        Console.WriteLine($"test with tag '{arg}'");

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

                driver.ExecuteTests(testConstructor);

                break;
        }
    }
}
