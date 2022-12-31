using DriverForTestsLib;

namespace Tests;
using static TestTaskTagCondition;

class Program
{
    static void Main(string[] args)
    {
        // Создаём объект, который будет управлять тестами
        var driver = new DriverForTests();
        Console.WriteLine($"args: " + args.Length);

        switch (args.Length)
        {
            // Если нет аргументов, то просто выполняем все тесты
            case 0:
                driver.ExecuteTests
                (
                    new TestConstructor[] { new Example1TestConstructor() }
                );
                break;

            // Если аргументы есть, то мы объединяем их всех через запятую и передаём в парсер
            // Парсер сформирует условия на выполнение тестовых задач исходя из аргументов программы
            // Переданные аргументы: теги на выполнение
            default:
                var testConstructor = new Example1TestConstructor();
                var parser          = new TestConditionParser
                                        (String.Join(',', args), true);

                testConstructor.conditions = parser.resultCondition;
                driver.ExecuteTests
                (
                    new TestConstructor[] { testConstructor }
                );

                break;
        }
    }
}
