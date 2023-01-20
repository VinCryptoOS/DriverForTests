/*
В этом файле мы определяем тестовые задачи
*/

// #define CAN_CREATEFILE_FOR_TestBytes


using System.Text;
using DriverForTestsLib;

namespace Tests;

// [TestTagAttribute("base", notAutomatic: true)]
class BaseExampleTestTask: TestTask
{
    public BaseExampleTestTask(TestConstructor constructor): base("", constructor)
    {
        this.Name = this.GetType().Name;
    }
}

// Некорректная задача: не имеет конструктора по умолчанию и не может быть поставлена автоматически в getTasksFromAppDomain
[TestTagAttribute]
class TestIncorrect_1: TestTask
{
    public TestIncorrect_1(string name, TestConstructor constructor): base(name, constructor)
    {}
}

// Эта задача будет автоматически поставлена в очередь задач при вызове getTasksFromAppDomain и addTasksForQueue
[TestTagAttribute("fast")]
class Test1_1: BaseExampleTestTask
{
    public Test1_1(TestConstructor constructor): base(constructor)
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
    public Test2_1(int number, TestConstructor constructor): base(nameof(Test2_1) + " №" + number, constructor)
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
    public TestSingleThread_1(int number, TestConstructor constructor): base(nameof(TestSingleThread_1) + " №" + number, constructor)
    {
        taskFunc = () =>
        {
            Console.WriteLine($"Задача {Name} началась");
            Thread.Sleep(1000);
            Console.WriteLine($"Задача {Name} закончилась");
        };
    }
}

// На эту задачу навешано сразу несколько тегов
[TestTagAttribute("fast",   double.MaxValue)]
[TestTagAttribute("slow",   double.MaxValue)]
[TestTagAttribute("medium", double.MaxValue)]
[TestTagAttribute("error", double.MaxValue)]
class TestSlowAndFastAndMedium_1: BaseExampleTestTask
{
    public TestSlowAndFastAndMedium_1(TestConstructor constructor): base(constructor)
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

[TestTagAttribute("PTT", notAutomatic: true)]
class ParallelTasks_Tests: ExampleAutoSaveTask_parent
{
    public IEnumerable<TestTask> getTasks()
    {
              int TasksInSequence  = 29;
        const int CountOfSequences = 12;

        for (int seq = 0; seq < CountOfSequences; seq++)
        {
            for (int i = 0; i  < TasksInSequence; i++)
            {
                var t = new Task(seq.ToString("D2"), this.constructor, taskNames);

                // Ставим обязательное ожидание окончания предыдущей последовательности
                t.waitAfter = i == TasksInSequence - 1;
                yield return t;
            }
        }

        // Последняя задача: тут выполняем сохранение
        // Она автоматически ставит себе ожидание окончания всех предыдущих задач
        yield return this;
    }

    public List<string> taskNames = new List<string>(1024);
    public ParallelTasks_Tests(TestConstructor constructor, bool canCreateFile = false):
                    base
                    (
                        name:               "ParallelTasks_Tests",
                        dirForFiles:        ExampleAutoSaveTask.getDirectoryPath(),
                        executer_and_saver: new Saver(),
                        constructor:        constructor
                    )
    {
        this.executer_and_saver.canCreateFile = canCreateFile;

        // Ждём выполнения всех задач перед этой задачей, чтобы объект был полностью сформирован
        this.waitBefore = true;
    }

    [TestTagAttribute("PTT", notAutomatic: true)]
    class Task : TestTask
    {
        public int SleepTime_In_ms = 125;
        public Task(string Name, TestConstructor? constructor, List<string> taskNames) : base(Name, constructor)
        {
            taskFunc = () =>
            {/* // Это комментируем, т.к. begun и ended переупорядочиваются 
                lock (taskNames)
                    if (waitAfter)
                        taskNames.Add("task begun waitAfter in sequence: " + Name);
                    else
                        taskNames.Add("task begun in sequence: " + Name);

                Thread.Sleep(SleepTime_In_ms);

                lock (taskNames)
                    if (waitAfter)
                        taskNames.Add("task ended waitAfter in sequence: " + Name);
                    else
                        taskNames.Add("task ended in sequence: " + Name);*/

                lock (taskNames)
                    taskNames.Add("task in sequence: " + Name);

                Thread.Sleep(SleepTime_In_ms);

                lock (taskNames)
                    taskNames.Add("task in sequence: " + Name);
            };
        }
    }


    protected class Saver: TaskResultSaver
    {
        public override object ExecuteTest(AutoSaveTestTask task)
        {
            if (task is ParallelTasks_Tests pTask)
                return pTask.taskNames;

            throw new Exception("ParallelTasks_Tests.Saver.ExecuteTest: task is not ParallelTasks_Tests");
        }
    }
}


class ExampleAutoSaveTask_parent: AutoSaveTestTask
{
    public ExampleAutoSaveTask_parent(string name, DirectoryInfo dirForFiles, TaskResultSaver executer_and_saver, TestConstructor constructor, bool canCreateFile = false):
                    base
                    (
                        name:               name,
                        dirForFiles:        ExampleAutoSaveTask.getDirectoryPath(),
                        executer_and_saver: executer_and_saver,
                        constructor:        constructor
                    )
    {
        this.executer_and_saver.canCreateFile = canCreateFile;
    }

    public static DirectoryInfo getDirectoryPath(string DirName = "autotests")
    {
        // var pathToFile = typeof(Program).Assembly.Location;
        var pathToFile = System.AppContext.BaseDirectory;
        var dir        = new DirectoryInfo(pathToFile)?.Parent?.Parent?.Parent?.Parent;
        if (dir == null)
            throw new Exception();

        return new DirectoryInfo(  Path.Combine(dir.FullName, DirName)  );
    }
}

[TestTagAttribute("autosave", double.MaxValue, notAutomatic: true)]
class ExampleAutoSaveTask: ExampleAutoSaveTask_parent
{
    public static IEnumerable<ExampleAutoSaveTask> getTasks(TestConstructor constructor, bool canCreateFile = false)
    {
        var plus  = new string[] {"+", "", "-"};

        foreach (var a1 in plus)
        foreach (var a2 in plus)
        foreach (var a3 in plus)
        foreach (var a4 in plus)
        foreach (var a5 in plus)
        {
            var p = $"{a1}1 {a2}2 {a3}3 {a4}4 {a5}5";
            var t = new ExampleAutoSaveTask(p, constructor, canCreateFile);

            yield return t;
        }

        var testStrs = new string[]
        {
            "", "+1", "-1", "1", "tag1", "+tag1", "++tag1", "--tag1", ",", "   ", ",,,"
        };
        foreach (var testStr in testStrs)
        {
            var t = new ExampleAutoSaveTask(testStr, constructor, canCreateFile);
            yield return t;
        }
    }

    public ExampleAutoSaveTask(string searchPattern, TestConstructor constructor, bool canCreateFile = false):
                    base
                    (
                        name:               "AutoSaveTask " + searchPattern,
                        dirForFiles:        ExampleAutoSaveTask.getDirectoryPath(),
                        executer_and_saver: new Saver(searchPattern),
                        constructor:        constructor
                    )
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


[TestTagAttribute("autosave")]
class TestBytes_TestTask: ExampleAutoSaveTask_parent
{
    public TestBytes_TestTask(TestConstructor constructor):
                    base
                    (
                        name:               "testBytes",
                        dirForFiles:        ExampleAutoSaveTask.getDirectoryPath(),
                        executer_and_saver: new Saver(),
                        constructor:        constructor
                    )
    {
        #if CAN_CREATEFILE_FOR_TestBytes
            this.executer_and_saver.canCreateFile = true;
            #warning CAN_CREATEFILE_FOR_TestBytes
        #else
            this.executer_and_saver.canCreateFile = false;
        #endif
    }

    protected class Saver: TaskResultSaver
    {
        public Saver()
        {}

        public static readonly string[] Str = {"Строка текста"};
        public override object ExecuteTest(AutoSaveTestTask task)
        {
            var lst = new List<byte[]>();

            lst.Add(new byte[] {});
            lst.Add(new byte[] {0});
            lst.Add(new byte[] {1});
            lst.Add(new byte[] {1, 2});
            lst.Add(new byte[] {1, 2, 3});

            for (int i = 0; i < 128; i++)
            {
                var b = new byte[i];
                lst.Add(b);

                for (int j = 0; j < b.Length; j++)
                    b[j] = (byte) (j - i);
            }

            for (int i = 0; i < Str.Length; i++)
            {
                lst.Add(Encoding.UTF32.GetBytes(Str[i]));
                lst.Add(Encoding.UTF8 .GetBytes(Str[i]));
            }

            return lst;
        }
    }
}

[TestTagAttribute("autosave")]
class TestObjects_TestTask: ExampleAutoSaveTask_parent
{
    public TestObjects_TestTask(TestConstructor constructor):
                    base
                    (
                        name:               "testObjects",
                        dirForFiles:        ExampleAutoSaveTask.getDirectoryPath(),
                        executer_and_saver: new Saver(),
                        constructor:        constructor
                    )
    {
        #if CAN_CREATEFILE_FOR_TestBytes
            this.executer_and_saver.canCreateFile = true;
            #warning CAN_CREATEFILE_FOR_TestBytes
        #else
            this.executer_and_saver.canCreateFile = false;
        #endif
    }

    protected class Saver: TaskResultSaver
    {
        public Saver()
        {}

        public static readonly string[] Str = {"Строка текста"};
        public override object ExecuteTest(AutoSaveTestTask task)
        {
            var lst = new List<object>();

            for (int i = 0; i < Str.Length; i++)
            {
                lst.Add(new int[] {0, 1, 2, 3, 4, 5, 65536, 1 << 16, 1 << 32, 1 << 63});
                lst.Add(Str[i]);
                lst.Add(Encoding.UTF32.GetBytes(Str[i]));
                lst.Add(Encoding.UTF8 .GetBytes(Str[i]));
                lst.Add(1);
                lst.Add((1, 2, 3, 4, 5, 189));
                lst.Add(((11, 12), 2, 3, 4, 5, (61, 62, 63)));
                lst.Add(new byte[,]  { {0, 1, 2}, {3, 4, 5} });
                lst.Add(new byte[][] { new byte[]{0, 1, 2}, new byte[]{} });
                lst.Add(new object[] { 1, new byte[]{0, 1, 2}, new int[]{0, 1, 2} });
            }

            return lst;
        }
    }
}
