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
    public int msToRefreshMessagesAtDisplay = 2000;                 /// <summary>Время в секундах, которое должна выполняться задача, чтобы вызвать своё отображение на консоли</summary>
    public int minSecondsToMessageAboutExecutedTask = 8;
                                                                    /// <summary>Шаблон имени лог-файла для LogFileName. Символ $ будет заменён датой и временем, полученной из функции HelperClass.DateToDateFileString</summary>
    public string? LogFileNameTempl = "tests-$.log";                /// <summary>Имя лог-файла, в который будет выведено время начала и конца задач, а также исключения, возникшие в ходе выполнения задач</summary><remarks>Генерируется автоматически из LogFileNameTempl</remarks>
    public string? LogFileName      = null;

    public readonly ref struct ExecuteTestsOptions
    {                                                                           /// <summary>После окончания тестов ожидать ввода Enter [Console.ReadLine()]</summary>
        public readonly bool doConsole_ReadLine       {get; init;}              /// <summary>Вести лог-файл</summary>
        public readonly bool doKeepLogFile            {get; init;}              /// <summary>До первого вывода ожидать n миллисекунд. Используется, чтобы дать возможность программисту прочитать сообщения, которые выдавались на консоль перед запуском тестов</summary>
        public readonly int  sleepInMs_ForFirstOutput {get; init;}              /// <summary>Макисмальное количество потоков, которое будет исползовано для одновременного запуска тестов</summary>
        public readonly int? maxThreadCount           {get; init;}

        public ExecuteTestsOptions()
        {}
    }

    public DateTime startTime {get; protected set;}

    /// <summary>Получает список тестов и выполняет их</summary>
    /// <param name="testConstructors">Список контрукторов тестов, которые сконструируют задачи</param>
    /// <param name="options">Дополнительные опции запуска тестов</param>
    /// <returns>Количество ошибок, найденных тестами. 0 - ошибок не найдено</returns>
    public int ExecuteTests(IEnumerable<TestConstructor> testConstructors, ExecuteTestsOptions options = default)
    {
        using var processPrioritySetter = new ProcessPrioritySetter(ProcessPriorityClass.Idle, true);

        var now     = DateTime.Now;
        startTime   = now;
        LogFileName = LogFileNameTempl?.Replace("$", HelperDateClass.DateToDateFileString(now));
        if (!options.doKeepLogFile)
            LogFileName = null;

        System.Collections.Concurrent.ConcurrentQueue<TestTask> AllTasks = new ConcurrentQueue<TestTask>();
        foreach (var testConstructor in testConstructors)
            testConstructor.CreateTasksLists(AllTasks);

        Object sync = new Object();
        int started = 0;            // Количество запущенных прямо сейчас задач
        int ended   = 0;            // Количество завершённых задач
        int errored = 0;            // Количество задач, завершённых с ошибкой
        int PC = options.maxThreadCount ?? Environment.ProcessorCount;

        // Это специально делается последовательно, чтобы сохранить порядок задач для выполнения
        var tasks = new ConcurrentQueue<TestTask>();
        foreach (var task in AllTasks)
        {
            if (task.ShouldBeExecuted())
                tasks.Enqueue(task);
        }


        foreach (var task in tasks)
        {
            var acceptableThreadCount = task.waitBefore ? 1 : PC;
            waitForTasks(options, acceptableThreadCount, true);

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
                    }
                    finally
                    {
                        Interlocked.Decrement(ref started);
                        Interlocked.Increment(ref ended);

                        if (task.error.Count > 0)
                            Interlocked.Increment(ref errored);

                        task.ended   = true;
                        task.done    = 100f;
                        task.endTime = DateTime.Now;

                        lock (sync)
                            Monitor.PulseAll(sync);

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
            waitForTasks(options, acceptableThreadCount, true);
        }

        waitForTasks(options, 1,     true);
        WaitMessages(options, false, true);

        var endTime = DateTime.Now;
        var endMsg  = "Tests ended in time " + HelperDateClass.TimeStampTo_HHMMSSfff_String(endTime - startTime) + "\t\t" + DateTime.Now.ToLongDateString() + "\t" + DateTime.Now.ToLongTimeString();
        Console.WriteLine(endMsg);

        if (LogFileName != null)
        lock (tasks)
            File.AppendAllText(LogFileName, "\n" + endMsg + "\n\n");

        if (options.doConsole_ReadLine)
        {
            Console.WriteLine("Press 'Enter' to exit");
            Console.ReadLine();
        }

        return errored;


        bool keyAvailable()
        {
            try
            {
                return Console.KeyAvailable;
            }
            catch
            {
                return false;
            }
        }

        void WaitMessages(ExecuteTestsOptions options, bool showWaitTasks = false, bool endedAllTasks = false)
        {
            var now = DateTime.Now;

            // Ожидаем задержку во времени первого вывода
            // Если это промежуточный вывод, и на консоли нет ввода, и время ожидания вывода ещё не истекло
            if (!endedAllTasks)
            if (options.sleepInMs_ForFirstOutput > 0 && !keyAvailable())
            if ((now - startTime).TotalMilliseconds < options.sleepInMs_ForFirstOutput)
                return;

            if (!endedAllTasks && (now - waitForTasks_lastDateTime).TotalMilliseconds < msToRefreshMessagesAtDisplay)
                return;

            waitForTasks_lastDateTime = now;

            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            Console.Clear();
            // Console.CursorLeft = 0;
            // Console.CursorTop  = 0;

            var sbEnd = PrintMainTaskState(ended, errored, tasks);

/*
            if (LogFileName != null)
                lock (tasks)
                    File.AppendAllText(LogFileName, sbEnd.ToString() + "\n\n");
*/
            if (showWaitTasks && ended != tasks.Count)
            {
                var sb = new StringBuilder();
                now = DateTime.Now;
                var cnt = 0;
                var cntToMessage = 0;

                foreach (var task in tasks)
                {
                    if (!task.ended && task.start)
                    {
                        cnt++;

                        var str = "";
                        // if (task.done > 0)
                        str = task.done.ToString("F0") + "%";

                        var ts  = now - task.started;
                        if (ts.TotalSeconds >= minSecondsToMessageAboutExecutedTask)
                            cntToMessage++;

                        var tss = HelperDateClass.TimeStampTo_HHMMSSfff_String(ts);
                        sb.AppendLine($"{str,3} {tss,15} {task.Name}\n");
                    }
                }

                if (cntToMessage > 0)
                {
                    sb.Insert(0, $"Выполняемые задачи: ({cnt})\t[{HelperDateClass.TimeStampTo_HHMMSSfff_String(now - startTime)}]\n");
                    Console.WriteLine(sb.ToString());
                }
            }
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            if (endedAllTasks)
            {
                foreach (var task in tasks)
                {
                    if (task.error.Count > 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine("ERRORS for task " + task.Name);
                        foreach (var e in task.error)
                        {
                            Console.WriteLine(e.Message);

                            if (LogFileName != null)
                            lock (tasks)
                            {
                                File.AppendAllText( LogFileName, $"ERROR in task {task.Name}\n{e.Message}\n{e?.ex?.StackTrace}\n\n" );
                            }
                        }
                    }
                }

                Console.WriteLine();
                PrintMainTaskState(ended, errored, tasks);
            }
        }

        void waitForTasks(ExecuteTestsOptions options, int acceptableThreadCount, bool showWaitTasks = false)
        {
            while (started >= acceptableThreadCount)
                lock (sync)
                {
                    Monitor.Wait(sync, msToRefreshMessagesAtDisplay);
                    WaitMessages(options, showWaitTasks);
                }
        }
    }

    private static StringBuilder PrintMainTaskState(int ended, int errored, ConcurrentQueue<TestTask> tasks)
    {
        var sbEnd = new StringBuilder(1024);
        sbEnd.AppendLine("Выполнено/всего: " + ended + " / " + tasks.Count);
        sbEnd.AppendLine("Задачи с ошибокй: " + errored);

        if (errored > 0)
            Console.BackgroundColor = ConsoleColor.Red;
        else
            Console.BackgroundColor = ConsoleColor.DarkGray;

        Console.WriteLine(sbEnd.ToString());

        Console.ResetColor();
        Console.WriteLine();

        return sbEnd;
    }
}
