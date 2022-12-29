# Использование тестов
Пример действий показан в подпапке Tests.

## Запуск тестов
1. Переопределить асбтрактный класс-конструктор тестов, например:
[Example1TestConstructor : TestConstructor](../Tests/Program_AddTasks.cs)

2. Реализовать в нём CreateTasksLists

3. Для автоматической постановки задач по атрибутам применить методы
    TestConstructor.getTasksFromAppDomain
    TestConstructor.addTasksForQueue
    
    как это показано в файле [../Tests/Program_AddTasks.cs](../Tests/Program_AddTasks.cs)

4. Для ручной постановки задач использовать "tasks.Enqueue(t)" в том же методе CreateTasksLists

5. Если нужно, необходимо определить в наследнике TestConstructor условие conditions на выполняемые тесты.
    Регистрация задач для тестов всегда полная, без фильтрации. Перед тестами проверяются conditions.

6. Для запуска тестов вызвать driver.ExecuteTests( new Example1TestConstructor() );
    где Example1TestConstructor - это класс-фабрика, в котором был переопределён метод CreateTasksLists

## Создание класса, определяющего задачу тестирования

1. Отнаследовать новую задачу на тест от класса TestTask. 
    Передать в base-конструктор имя задачи. Это имя можно потом изменить, если это нужно

2. Если нужно, поставить на задачу атрибут TestTagAttribute
Например:
[TestTagAttribute("", double.MaxValue, singleThread: true, notAutomatic: true)]
class TestSingleThread_1: TestTask

Атрибут можно поставить несколько раз

3. Написать для задачи taskFunc

4. Если тест завершается с ошибой, он должен добавить ошибку в свой список ошибок
    var error = new TestError();
    this.error.Add(error);

    Пример генерации ошибки тестирования есть в файле [../Tests/ExampleTasks.cs](../Tests/ExampleTasks.cs) в классе TestSlowAndFastAndMedium_1

    Генерация необработанного исключения в задаче автоматически регистрирует ошибку

