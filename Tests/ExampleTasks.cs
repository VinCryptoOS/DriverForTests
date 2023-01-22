/*
В этом файле мы определяем тестовые задачи
*/

// #define CAN_CREATEFILE_FOR_TestBytes
// #define CAN_CREATEFILE_FOR_conditions
// #define CAN_CREATEFILE_FOR_AUTOSAVE


using System.Collections.Concurrent;
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

[TestTagAttribute("autosave")]
class ExampleAutoSaveTask: ExampleAutoSaveTask_parent
{
    /// <summary>Генерирует шаблоны поиска для тестирования</summary>
    public static void addPattern(List<string> result, string[] strs, int index)
    {
        if (index >= strs.Length)
            return;

        var variants = new string?[] { null, "", "-", "+", "<1 " };

        foreach (var v in variants)
        {
            // Подготавливаем описатель нашей задачи: тега index либо нет, либо он есть в разных вариантах
            var tagName =  v != null ? strs[index] : "";
            if (!String.IsNullOrEmpty(tagName))
                tagName = v + tagName;

            var r = new List<string>(256);
            addPattern(r, strs, index + 1);

            if (r.Count > 0)
            foreach (var rv in r)
            {
                var name = $"{tagName, 3} {rv}";
                result.Add(name);
            }
            else
            {
                result.Add($"{tagName, 3}");
            }
        }
    }

    public static IEnumerable<string> getPatterns()
    {
        var strs = TestConstructor_AU.tasksNamesTags;

        var result = new List<string>(256);
        result.Add("");
        result.Add("1");
        result.Add("-1");
        result.Add("<0 1");
        result.Add("<1 1");
        result.Add("<2 1");
        result.Add("1 <0 -1");
        result.Add("1 <1 -1");
        result.Add("<0 -1");
        result.Add("? <0 -1");
        result.Add("<1 -1");
        result.Add("? <1 -1");
        result.Add("<2 -1");
        result.Add("? <2 -1");
        result.Add("1 <1 -1");
        result.Add("1 <2 -1");
        result.Add("-1 <0 1");
        result.Add("-1 <1 1");
        result.Add("-1 <2 1");
        result.Add("1 <0 -2");
        result.Add("1 <1 -2");
        result.Add("-2 <0 1");
        result.Add("-2 <1 1");
        result.Add("1 <2 -2");
        result.Add("-2 <2 1");

        result.Add("+1 -2 -3");
        result.Add("-2 -3 <0 +1");
        result.Add("-2 -3 <1 +1");
        result.Add("-1 -2 <1 -4");

        addPattern(result, strs, 0);

        return result;
    }

    /// <summary>Конструктор тестовых задач для TestTask_Conditions_Main</summary>
    public class TestConstructor_AU: TestConstructor
    {
        public TestConstructor_AU(): base()
        {}

        /// <summary>Генерирует шаблоны пустых (незапускаемых) задач для тестирования</summary>
        public void addTaskTag(List<TestTask_Conditions> result, string[] strs, int index)
        {
            if (index >= strs.Length)
                return;

            var variants = new bool[] { true, false };

            foreach (var v in variants)
            {
                // Подготавливаем описатель нашей задачи: тега index либо нет, либо он есть в разных вариантах
                var tagName =  v ? strs[index] : "";

                var r = new List<TestTask_Conditions>(256);
                addTaskTag(r, strs, index + 1);

                if (r.Count > 0)
                foreach (var rv in r)
                {
                    var name = $"{tagName, 3} {rv.Name}";

                    var task = new TestTask_Conditions(name, this);
                    if (v)
                        task.tags.Add(new TestTaskTag(tagName, -1d, 1d));

                    task.tags.AddRange(rv.tags);

                    result.Add(task);
                }
                else
                {
                    var name = $"{tagName, 3}";

                    var task = new TestTask_Conditions(name, this);
                    if (v)
                        task.tags.Add(new TestTaskTag(tagName, -1d, 1d));

                    result.Add(task);
                }
            }
        }

        public static string[] tasksNamesTags = new string[] { "1", "2", "3", "4", "5" };
        public IEnumerable<TestTask_Conditions> getTasks()
        {
            var strs = tasksNamesTags;

            var result = new List<TestTask_Conditions>(256);
            addTaskTag(result, strs, 0);

            return result;
        }

        public override void CreateTasksLists(ConcurrentQueue<TestTask> tasks)
        {
            TestConstructor.addTasksForQueue(getTasks(), tasks);
        }
    }

    public ExampleAutoSaveTask(TestConstructor constructor):
                    base
                    (
                        name:               "AutoSaveTask_parser",
                        dirForFiles:        ExampleAutoSaveTask.getDirectoryPath(),
                        executer_and_saver: new Saver(),
                        constructor:        constructor
                    )
    {
        this.executer_and_saver.canCreateFile = false;
        #if CAN_CREATEFILE_FOR_AUTOSAVE
            #warning CAN_CREATEFILE_FOR_AUTOSAVE
            this.executer_and_saver.canCreateFile = true;
        #endif
    }

    protected class Saver: TaskResultSaver
    {
        public override object ExecuteTest(AutoSaveTestTask generalTask)
        {
            var lst    = new List<(string searchPattern, List<string> tasks)>(256);
            var tasks  = new ConcurrentQueue<TestTask>();
            var tc     = new TestConstructor_AU();

            tc.CreateTasksLists(tasks);

            foreach (var searchPattern in getPatterns())
            {
                var parser    = new TestConditionParser(searchPattern);
                tc.conditions = parser.resultCondition;

                var L = new List<string>(128);
                lst.Add((searchPattern, L));

                foreach (var task in tasks)
                    if (tc.ShouldBeExecuted(task))
                    {
                        L.Add(task.Name);
                    }
            }

            return lst;
        }
    }
}

// -------------------------------- Тестирование условий --------------------------------
/// <summary>Конструктор тестовых задач для TestTask_Conditions_Main</summary>
public class TestConstructor_Conditions: TestConstructor
{
    public TestConstructor_Conditions(): base()
    {}

    public IEnumerable<TestTask_Conditions> getTasks()
    {
        var dbl = new double[] {-1d, 0d, 1d, 2d, 9d};

        foreach (var db1 in dbl)
        {
            var a = new TestTask_Conditions($"1   {db1,2}", this);
            a.tags.Add(new TestTaskTag("1", -1d, db1));
            yield return a;

            a = new TestTask_Conditions($"2   {db1,2}", this);
            a.tags.Add(new TestTaskTag("2", -1d, db1));
            yield return a;

            a = new TestTask_Conditions($"3   {db1,2}", this);
            a.tags.Add(new TestTaskTag("3", -1d, db1));
            yield return a;

            a = new TestTask_Conditions($"4   {db1,2}", this);
            a.tags.Add(new TestTaskTag("4", -1d, db1));
            yield return a;

            foreach (var db2 in dbl)
            {
                a = new TestTask_Conditions($"1 2 {db1,2} {db2,2}", this);
                a.tags.Add(new TestTaskTag("1", -1d, db1));
                a.tags.Add(new TestTaskTag("2", -1d, db2));
                yield return a;
            }
        }
    }

    public override void CreateTasksLists(ConcurrentQueue<TestTask> tasks)
    {
        TestConstructor.addTasksForQueue(getTasks(), tasks);
    }
}

// Тестовая задача для TestTask_Conditions_Main и для задачи ExampleAutoSaveTask
public class TestTask_Conditions: TestTask
{
    public TestTask_Conditions(string name, TestConstructor constructor): base(name, constructor)
    {}
}

[TestTagAttribute("autosave")]
class TestTask_Conditions_Main: ExampleAutoSaveTask_parent
{
    public TestTask_Conditions_Main(TestConstructor constructor):
                        base
                        (
                            name:               "TestTask_Conditions_Main",
                            dirForFiles:        ExampleAutoSaveTask.getDirectoryPath(),
                            executer_and_saver: new Saver(),
                            constructor:        constructor
                        )
    {
        #if CAN_CREATEFILE_FOR_conditions
            this.executer_and_saver.canCreateFile = true;
            #warning CAN_CREATEFILE_FOR_conditions
        #else
            this.executer_and_saver.canCreateFile = false;
        #endif
    }

    protected class Saver: TaskResultSaver
    {
        public Saver()
        {}

        class TestResult
        {
            public class ResultTasks
            {
                public readonly string       conditionString;
                public readonly List<string> tasks = new List<string>(128);

                public ResultTasks(string conditionString) => this.conditionString = conditionString;
            }

            public readonly List<ResultTasks> results = new List<ResultTasks>(128);
        }

        public override object ExecuteTest(AutoSaveTestTask task)
        {
            var result = new TestResult();

            System.Collections.Concurrent.ConcurrentQueue<TestTask> AllTasks = new ConcurrentQueue<TestTask>();
            var c = new TestConstructor_Conditions();
            c.CreateTasksLists(AllTasks);

            addTaskGroup("1 <2 -1",             result, AllTasks, c);

            addTaskGroup("",                    result, AllTasks, c);
            addTaskGroup("?",                   result, AllTasks, c);
            addTaskGroup("3",                   result, AllTasks, c);
            addTaskGroup("1",                   result, AllTasks, c);
            addTaskGroup("1 ?",                 result, AllTasks, c);
            addTaskGroup("1 2",                 result, AllTasks, c);
            addTaskGroup("3 4",                 result, AllTasks, c);
            addTaskGroup("1 2 3 4",             result, AllTasks, c);
            addTaskGroup("-3",                  result, AllTasks, c);
            addTaskGroup("-1",                  result, AllTasks, c);
            addTaskGroup("-1 ?",                result, AllTasks, c);
            addTaskGroup("-1 <1 ?",             result, AllTasks, c);
            addTaskGroup("1 <0 2 <1 3 <2 4",    result, AllTasks, c);
            addTaskGroup("1 <2 2 <1 3 <0 4",    result, AllTasks, c);
            addTaskGroup("1 <2 2 <1 3 <0 -4",   result, AllTasks, c);
            addTaskGroup("1 -4 <0 2 <1 3 <2 ?", result, AllTasks, c);
            addTaskGroup("1 <1 ?",              result, AllTasks, c);
            addTaskGroup("1 <3 4",              result, AllTasks, c);
            addTaskGroup("? <1 1",              result, AllTasks, c);
            addTaskGroup("+1 <1 4 <2 3",        result, AllTasks, c);
            addTaskGroup("+4 -1 <0  1",         result, AllTasks, c);
            addTaskGroup("+4 -1 <0 +1",         result, AllTasks, c);
            addTaskGroup("+1 -4 <0 +4",         result, AllTasks, c);

            return result;
        }

        private static void addTaskGroup(String conditionString, TestResult result, ConcurrentQueue<TestTask> AllTasks, TestConstructor_Conditions c)
        {
            c.conditions = new TestConditionParser(conditionString).resultCondition;

            var rt = new TestResult.ResultTasks(conditionString);
            result.results.Add(rt);

            foreach (var t in AllTasks)
            {
                if (t.ShouldBeExecuted())
                    rt.tasks.Add(t.Name);
            }
        }
    }
}


// --------------------------------   --------------------------------

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
