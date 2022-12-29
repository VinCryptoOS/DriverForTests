# Общий алгоритм
Общий алгоритм использования описан в самой библиотеке в файле [../DriverForTestsLib/README.md](../DriverForTestsLib/README.md)


# Пример
В примере определены задачи без тега и с тегами "base", "fast", "medium", "slow"
Задача с тегом "base" закомментирована (раскомментируйте для того, чтобы получить "забытую" задачу)

Задачи определены в файле [ExampleTasks.cs](ExampleTasks.cs)
Сбор задач из сборки в [Program_AddTasks.cs](Program_AddTasks.cs)
Запуск тестов в файле [Program.cs](Program.cs)

Пример генерации ошибки тестирования есть в файле [ExampleTasks.cs](ExampleTasks.cs) в классе TestSlowAndFastAndMedium_1

Пример реализации задачи автосохраняемых объектов также в [ExampleTasks.cs](ExampleTasks.cs) , см. класс ExampleAutoSaveTask. См. флаг canCreateFile = true для первой генерации файлов
Запуск только автосохраняемых задач можно сделать командой
dotnet publish/Tests.dll autosave


В файле [README.txt](README.txt) обсуждён вывод тестового примера.

Вызов программы осуществляется командой по шаблону
dotnet publish/Tests.dll &> log

При этом можно передавать в качестве параметров теги для фильтрации тестов
Например, мы хотим выполнить только тесты с тегом slow

dotnet publish/Tests.dll slow &> log


Мы хотим выполнить все задачи slow, кроме помеченных ещё и тегом medium

dotnet publish/Tests.dll slow -medium &> log


Мы хотим выполнить все задачи medium, кроме задач помеченных тегом slow. Однако, мы хотим выполнить даже те задачи medium, которые помечены slow

dotnet publish/Tests.dll +medium -slow &> log


Для выполнения задач, у которых есть не менее двух каких-либо тегов из заданных, уже надо писать отдельные условия внутри программы: через параметры задать такие условия не получится.
