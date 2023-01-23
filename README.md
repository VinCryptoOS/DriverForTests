# DriverForTests

For [.NET 7.0](https://dotnet.microsoft.com/download)

Система управления тестами для [VinKekFish](https://github.com/VinKekFish/VinKekFish)
(может использоваться отдельно).

* Позволяет запускать тесты параллельно и, если необходимо, то последовательно избранные тесты
* Позволяет фильтровать тесты по тегам

Каждый тест должен быть вручную оформлен как класс-наследник TestTask

## Билд и извлечение:
Если нужно использовать совместно с VinKekFish - см. build [VinKekFish](https://github.com/VinKekFish/VinKekFish)

В каталоге, где нужно создать директорию
git clone https://github.com/VinCryptoOS/DriverForTests
cd DriverForTests
dotnet publish -c Release

После билда автоматически запускаются тесты
Если всё в порядке, тесты заканчиваются надписью
Задачи с ошибокй: 0
Tests ended in time ...


# [./DriverForTestsLib](./DriverForTestsLib)

Библиотека для управления тестами
[README.md](./DriverForTestsLib/README.md)

# [./Tests](./Tests)

Тесты для библиотеки тестов: примеры использования
[README.md](./Tests/README.md)
