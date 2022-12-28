using DriverForTestsLib;

namespace Tests;


class Program
{
    static void Main(string[] args)
    {
        var driver = new DriverForTests();
        
        driver.ExecuteTests( new ExampleTestConstructor() );
    }
}
