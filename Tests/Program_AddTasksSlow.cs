// #define doPerformaceTest

using System.Collections.Concurrent;
using System.Diagnostics;
using DriverForTestsLib;

namespace Tests;


class Example1SlowTestConstructor : Example1TestConstructor
{
    public Example1SlowTestConstructor()
    {
        this.ProcessPriority = ProcessPriorityClass.Idle;
    }

    public override void CreateTasksLists(ConcurrentQueue<TestTask> tasks)
    {
        
        base.CreateTasksLists(tasks);
    }
}
