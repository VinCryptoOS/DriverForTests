// #define doPerformaceTest

using System.Collections.Concurrent;
using System.Diagnostics;
using DriverForTestsLib;

namespace Tests;


class Example1TestConstructor : TestConstructor
{
    public Example1TestConstructor()
    {
        this.ProcessPriority = ProcessPriorityClass.Idle;
    }

    public override void CreateTasksLists(ConcurrentQueue<TestTask> tasks)
    {
        Console.WriteLine("ExampleTestConstructor.CreateTasksLists executed");

        // Эти задачи поставлены вручную, будут выполняться параллельно
        for (int i = 0; i < Environment.ProcessorCount*3; i++)
        {
            var t = new Test2_1(i);
            tasks.Enqueue(t);
        }

        // Эти задачи поставлены вручную, будут выполняться последовательно, одна за другой
        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            var t = new TestSingleThread_1(i);
            tasks.Enqueue(t);
        }

        // Получаем все задачи, которые могут быть автоматически собраны из данного домена приложения
        var list = TestConstructor.getTasksFromAppDomain
        (
            (Type t, bool notAutomatic) =>
            {
                if (!notAutomatic)
                    Console.Error.WriteLine("Incorrect task: " + t.FullName);
                else
                {
                    foreach (var task in tasks)
                    {
                        var taskType = task.GetType();
                        if (t == taskType)
                            return;
                    }

                    Console.Error.WriteLine($"A notAutomatic task has been declared, but it is not in the list for execution: {t.FullName}");
                }
            }
        );
        // Ставим эти задачи на выполнение
        TestConstructor.addTasksForQueue(list, tasks);

        Console.WriteLine("\n\nExampleTestConstructor.CreateTasksLists ended\n");
    }
}
