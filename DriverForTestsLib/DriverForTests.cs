using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace DriverForTestsLib;

/*
    Запуск DriverForTests.Main даёт выполнение тестов
    Класс TestConstructor tests должен быть отнаследован.
    Тесты должны быть вручную добавлены в этом классе в функции CreateTasksLists

*/


public class DriverForTests
{                                                                    /// <summary>Наиболее позднее время вывода на консоль информации о состоянии задач</summary><remarks>Не нужно пользователю</remarks>
    public DateTime waitForTasks_lastDateTime {get; protected set;} = default;
                                                                    /// <summary>Время в миллисекундах для обновления состояния задач на консоли</summary>
    public int msToRefreshMessagesAtDisplay = 2000;
                                                                    /// <summary>Шаблон имени лог-файла для LogFileName. Символ $ будет заменён датой и временем, полученной из функции HelperClass.DateToDateFileString</summary>
    public string? LogFileNameTempl = "tests-$.log";                /// <summary>Имя лог-файла, в который будет выведено время начала и конца задач, а также исключения, возникшие в ходе выполнения задач</summary><remarks>Генерируется автоматически из LogFileNameTempl</remarks>
    public string? LogFileName      = null;

    public int ExecuteTests(TestConstructor tests)
    {
        using var processPrioritySetter = new ProcessPrioritySetter(tests.ProcessPriority, true);

        var now       = DateTime.Now;
        var startTime = now;
        LogFileName   = LogFileNameTempl?.Replace("$", HelperDateClass.DateToDateFileString(now));

        System.Collections.Concurrent.ConcurrentQueue<TestTask> AllTasks = new ConcurrentQueue<TestTask>();
        tests.CreateTasksLists(AllTasks);

        Object sync = new Object();
        int started = 0;            // Количество запущенных прямо сейчас задач
        int ended   = 0;            // Количество завершённых задач
        int errored = 0;            // Количество задач, завершённых с ошибкой
        int PC = Environment.ProcessorCount;

        var tasks = new ConcurrentQueue<TestTask>();
        Parallel.ForEach<TestTask>
        (
            AllTasks,
              (TestTask task, ParallelLoopState pls, long index) =>
            {
                if (tests.ShouldBeExecuted(task))
                    tasks.Enqueue(task);
            }
        );


        foreach (var task in tasks)
        {
            var acceptableThreadCount = task.waitBefore ? 1 : PC;
            waitForTasks(acceptableThreadCount, true);

            Interlocked.Increment(ref started);
            ThreadPool.QueueUserWorkItem
            (
                delegate
                {
                    try
                    {
                        task.started = DateTime.Now;
                        task.start   = true;
                        task.taskFunc();
                    }
                    catch (Exception e)
                    {
                        task.error.Add(new TestError() { ex = e, Message = "During the test the exception occured\n" + e.Message });

                        if (LogFileName != null)
                        lock (tasks)
                        {
                            File.AppendAllText(LogFileName, $"error with task {task.Name}\n{e.Message}\n" );
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref started);
                        Interlocked.Increment(ref ended);
                        task.ended = true;

                        if (task.error.Count > 0)
                            Interlocked.Increment(ref errored);

                        lock (sync)
                            Monitor.PulseAll(sync);

                        task.endTime = DateTime.Now;

                        if (LogFileName != null)
                        lock (tasks)
                        {
                            File.AppendAllText(LogFileName, "task " + task.Name + "\n");
                            File.AppendAllText(LogFileName, "task started at " + HelperDateClass.DateToDateString(task.started) + "\n");
                            File.AppendAllText(LogFileName, "task ended   at " + HelperDateClass.DateToDateString(task.endTime) + "\n\n");
                        }
                    }
                }
            );

            acceptableThreadCount = task.waitAfter ? 1 : PC;
            waitForTasks(acceptableThreadCount, true);
        }

        waitForTasks(1,     true);
        WaitMessages(false, true);

        var endTime = DateTime.Now;
        Console.WriteLine("Tests ended in time " + HelperDateClass.TimeStampTo_HHMMSSfff_String(endTime - startTime));
        if (tests.Console_ReadLine)
        {
            Console.WriteLine("Press 'Enter' to exit");
            Console.ReadLine();
        }

        return errored;




        void WaitMessages(bool showWaitTasks = false, bool endedAllTasks = false)
        {
            if (!endedAllTasks && (DateTime.Now - waitForTasks_lastDateTime).TotalMilliseconds < msToRefreshMessagesAtDisplay)
                return;

            waitForTasks_lastDateTime = DateTime.Now;

            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            Console.Clear();
            // Console.CursorLeft = 0;
            // Console.CursorTop  = 0;
            Console.WriteLine("Выполнено/всего: " + ended + " / " + tasks.Count);
            Console.WriteLine("Задачи с ошибокй: " + errored);
            Console.WriteLine();

            if (showWaitTasks && ended != tasks.Count)
            {
                var sb  = new StringBuilder();
                    now = DateTime.Now;
                var cnt = 0;

                foreach (var task in tasks)
                {
                    if (!task.ended && task.start)
                    {
                        var str = "";
                        // if (task.done > 0)
                        str = task.done.ToString("F0") + "%";

                        var ts = HelperDateClass.TimeStampTo_HHMMSSfff_String(now - task.started);
                        sb.AppendLine($"{str, 3} {ts, 15} {task.Name}\n");
                        cnt++;
                    }
                }

                sb.Insert(0, $"Выполняемые задачи: ({cnt})\t[{HelperDateClass.TimeStampTo_HHMMSSfff_String(now - startTime)}]\n");
                Console.WriteLine(sb.ToString());
            }
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            if (endedAllTasks)
            {
                foreach (var task in tasks)
                {
                    if (task.error.Count > 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine("For task " + task.Name);
                        foreach (var e in task.error)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                }
            }
        }

        void waitForTasks(int acceptableThreadCount, bool showWaitTasks = false)
        {
            while (started >= acceptableThreadCount)
                lock (sync)
                {
                    Monitor.Wait(sync, msToRefreshMessagesAtDisplay);
                    WaitMessages(showWaitTasks);
                }
        }
    }
}
