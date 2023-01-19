# DriverForTests

For [.NET 7.0](https://dotnet.microsoft.com/download)

Система управления тестами.

* Позволяет запускать тесты параллельно и, если необходимо, то последовательно избранные тесты
* Позволяет фильтровать тесты по тегам

Каждый тест должен быть вручную оформлен как класс-наследник TestTask

Билд:
dotnet publish -c Release

После билда автоматически запускаются тесты


# [./DriverForTestsLib](./DriverForTestsLib)

Библиотека для управления тестами
[README.md](./DriverForTestsLib/README.md)

# [./Tests](./Tests)

Тесты для библиотеки тестов: примеры использования
[README.md](./Tests/README.md)
