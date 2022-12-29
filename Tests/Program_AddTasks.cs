/*
    Здесь мы переопределяем TestConstructor
    В этом файле мы ставим в список на выполнение конкретные тестовые задачи
*/

using System.Collections.Concurrent;
using System.Diagnostics;
using DriverForTestsLib;

namespace Tests;

/// <summary>Определяем класс-наследлник TestConstructor.
/// Этот класс регистрирует тестовые задачи на выполнение.
/// Регистрация идёт без фильтрации.
/// Фильтрация по тегам идёт непосредственно перед выполнением</summary>
class Example1TestConstructor : TestConstructor
{
    public Example1TestConstructor()
    {
        this.ProcessPriority = ProcessPriorityClass.Idle;
    }

    /// <summary>Этот метод регистрирует необходимые тестовые задачи на выполнение</summary>
    /// <param name="tasks">Список задач, который будет заполнен данным методом</param>
    public override void CreateTasksLists(ConcurrentQueue<TestTask> tasks)
    {
        Console.WriteLine("ExampleTestConstructor.CreateTasksLists executed");

        // Эти задачи поставлены вручную, будут выполняться параллельно
        for (int i = 0; i < Environment.ProcessorCount*3; i++)
        {
            var t = new Test2_1(i);
            tasks.Enqueue(t);
        }

        // Эти задачи поставлены вручную,
        // они будут выполняться последовательно, одна за другой,
        // т.к. на них определён атрибут с параметром singleThread: true
        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            var t = new TestSingleThread_1(i);
            tasks.Enqueue(t);
        }

        var canCreateFile = false;
        // Добавляем вручную задачи, реализующие AutoSaveTestTask
        // Если надо, устанавливаем разрешение на запись в файл в первый раз
        // #warning canCreateFile: true
        // canCreateFile = true;
        foreach (var t in ExampleAutoSaveTask.getTasks(canCreateFile: canCreateFile))
        {
            tasks.Enqueue(t);
        }

        // Получаем все задачи, которые могут быть автоматически собраны из данного домена приложения
        var list = TestConstructor.getTasksFromAppDomain
        (
            // Этот обработчик срабатывает тогда, когда задача либо неавтоматическая,
            // либо не имеет нужного конструктора
            // Здесь мы также проверяем, что мы не забыли поставить ручные (неавтоматические) задачи
            (Type t, bool notAutomatic) =>
            {
                if (!notAutomatic)
                    Console.Error.WriteLine("Incorrect task: " + t.FullName);
                else
                {
                    // Проверяем ручные задачи, что мы никакую не забыли поставить
                    // Так как все ручные задачи мы уже поставили перед вызовом getTasksFromAppDomain
                    // здесь если задача не зарегистрирована на выполнение, то это значит, что мы её забыли
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
