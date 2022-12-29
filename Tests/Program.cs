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
                var parser          = new TestConditionParser
                                        (String.Join(',', args));

                testConstructor.conditions = parser.resultCondition;

                driver.ExecuteTests(testConstructor);

                break;
        }
    }
}
