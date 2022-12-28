using DriverForTestsLib;

namespace Tests;

// Некорректная задача: не имеет конструктора по умолчанию и не может быть поставлена автоматически в getTasksFromAppDomain
[TestTagAttribute("", double.MaxValue)]
class TestIncorrect_1: TestTask
{
    public TestIncorrect_1(string name): base(name)
    {}
}

// Эта задача будет автоматически поставлена в очередь задач при вызове getTasksFromAppDomain и addTasksForQueue
[TestTagAttribute("")]
[TestTagAttribute("fast")]
class Test1_1: TestTask
{
    public Test1_1(): base(nameof(Test1_1))
    {
        taskFunc = () =>
        {
            Console.WriteLine("Задача Test1_1 началась");
            Thread.Sleep(1000);
            Console.WriteLine("Задача Test1_1 закончилась");
        };
    }
}

// Эта задача не имеет атрибута, поэтому не будет автоматически поставлена
// [TestTagAttribute("", double.MaxValue)]
class Test2_1: TestTask
{
    public Test2_1(int number): base(nameof(Test2_1) + " №" + number)
    {
        taskFunc = () =>
        {
            Console.WriteLine($"Задача {this.Name} началась");
            Thread.Sleep(1000);
            Console.WriteLine($"Задача {Name} закончилась");
        };
    }
}

// Это задача будет выполняться без многопоточности: ей будет отдан весь процессор целиком
[TestTagAttribute("", double.MaxValue, singleThread: true, notAutomatic: true)]
[TestTagAttribute("slow", double.MaxValue)]
class TestSingleThread_1: TestTask
{
    public TestSingleThread_1(int number): base(nameof(TestSingleThread_1) + " №" + number)
    {
        taskFunc = () =>
        {
            Console.WriteLine($"Задача {Name} началась");
            Thread.Sleep(1000);
            Console.WriteLine($"Задача {Name} закончилась");
        };
    }
}

[TestTagAttribute("fast", double.MaxValue)]
[TestTagAttribute("slow", double.MaxValue)]
[TestTagAttribute("medium", double.MaxValue)]
class TestSlowAndFastAndMedium_1: TestTask
{
    public TestSlowAndFastAndMedium_1(): base(nameof(TestSlowAndFastAndMedium_1))
    {
        taskFunc = () =>
        {
            Console.WriteLine($"Задача {Name} началась");
            Thread.Sleep(1000);
            Console.WriteLine($"Задача {Name} закончилась");
        };
    }
}
