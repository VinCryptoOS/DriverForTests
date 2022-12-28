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

        // Получаем все задачи, которые могут быть автоматически собраны из данного домена приложения
        var list = TestConstructor.getTasksFromAppDomain
        (
            (Type t) =>
            {
                Console.Error.WriteLine("Incorrect task: " + t.FullName);
            }
        );
        // Ставим эти задачи на выполнение
        TestConstructor.addTasksForQueue(list, tasks);

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

        Console.WriteLine("\n\nExampleTestConstructor.CreateTasksLists ended\n");
    }
}
