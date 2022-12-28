// #define doPerformaceTest

using System.Collections.Concurrent;
using System.Diagnostics;
using DriverForTestsLib;

namespace Tests;


class ExampleTestConstructor : TestConstructor
{
    public ExampleTestConstructor()
    {
        this.ProcessPriority = ProcessPriorityClass.Idle;
    }

    public override void CreateTasksLists(ConcurrentQueue<TestTask> tasks)
    {
        // Console.WriteLine("CreateTasksLists executed");
        
    }
}
