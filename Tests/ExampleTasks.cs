/*
В этом файле мы определяем тестовые задачи
*/

using DriverForTestsLib;

namespace Tests;

// [TestTagAttribute("base", notAutomatic: true)]
class BaseExampleTestTask: TestTask
{
    public BaseExampleTestTask(): base("")
    {
        this.Name = this.GetType().Name;
    }
}

// Некорректная задача: не имеет конструктора по умолчанию и не может быть поставлена автоматически в getTasksFromAppDomain
[TestTagAttribute]
class TestIncorrect_1: TestTask
{
    public TestIncorrect_1(string name): base(name)
    {}
}

// Эта задача будет автоматически поставлена в очередь задач при вызове getTasksFromAppDomain и addTasksForQueue
[TestTagAttribute("fast")]
class Test1_1: BaseExampleTestTask
{
    public Test1_1(): base()
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
[TestTagAttribute(singleThread: true, notAutomatic: true)]
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

// На эту задачу навешано сразу три тега
[TestTagAttribute("fast",   double.MaxValue)]
[TestTagAttribute("slow",   double.MaxValue)]
[TestTagAttribute("medium", double.MaxValue)]
class TestSlowAndFastAndMedium_1: BaseExampleTestTask
{
    public TestSlowAndFastAndMedium_1(): base()
    {
        taskFunc = () =>
        {
            Console.WriteLine($"Задача {Name} началась");
            Thread.Sleep(1000);

            var error = new TestError();
            this.error.Add(error);
            error.Message = "Это тестовая ошибка. Если в задаче возникнет исключение, такая ошибка будет сформирована автоматически";

            Console.WriteLine($"Задача {Name} закончилась");
        };
    }
}

[TestTagAttribute("autosave", double.MaxValue, notAutomatic: true)]
class ExampleAutoSaveTask: AutoSaveTestTask
{
    public static IEnumerable<ExampleAutoSaveTask> getTasks(bool canCreateFile = false)
    {
        var strs = new List<ExampleAutoSaveTask>(128);
        var plus = new string[] {"+", "", "-"};

        foreach (var a1 in plus)
        foreach (var a2 in plus)
        foreach (var a3 in plus)
        {
            var p = $"{a1}1 {a2}2 {a3}3";
            var t = new ExampleAutoSaveTask(p, canCreateFile);
            strs.Add(t);
        }

        return strs;
    }

    public ExampleAutoSaveTask(string searchPattern, bool canCreateFile = false): base("AutoSaveTask " + searchPattern, new DirectoryInfo("./autotests/"), new Saver(searchPattern))
    {
        this.executer_and_saver.canCreateFile = canCreateFile;
    }

    protected class Saver: TaskResultSaver
    {
        public readonly string searchPattern;
        public Saver(string searchPattern)
        {
            this.searchPattern = searchPattern;
        }

        public override object ExecuteTest(AutoSaveTestTask task)
        {
            var parser = new TestConditionParser(String.Join(',', searchPattern));

            return parser;
        }
    }
}
