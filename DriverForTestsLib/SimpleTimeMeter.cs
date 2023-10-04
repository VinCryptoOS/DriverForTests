namespace DriverForTestsLib;

/// <summary>Помогает легко замерить время между двумя точками кода</summary>
public class SimpleTimeMeter : IDisposable
{
    public readonly DateTime start;
    public          DateTime end   {get; protected set;}
    public SimpleTimeMeter()
    {
        start = DateTime.Now;
        end   = default;
    }

    /// <summary>Заканчивает измерение времени. Можно вызывать повторно без изменения интервала времени</summary>
    public void Dispose()
    {
        if (end == default)
        {
            end = DateTime.Now;
            setTimeSpan();
        }
    }

    public class NotEndedException: Exception
    {}

    protected TimeSpan ts = default;
    protected double   totalMilliseconds = -1d;
    protected double   totalSeconds      = -1d;

    /// <summary>Возвращает полное количество миллисекунд. Если задача не завершена, вызывает исключение NotEndedException</summary>
    public double TotalMilliseconds
    {
        get
        {
            return totalMilliseconds;
        }
    }

    /// <summary>Возвращает полное количество секунд. Если задача не завершена, вызывает исключение NotEndedException</summary>
    public double TotalSeconds
    {
        get
        {
            return totalSeconds;
        }
    }

    protected void setTimeSpan()
    {
        if (ts != default)
            return;

        if (end == default)
            throw new NotEndedException();

        ts = (end - start);
        totalMilliseconds = ts.TotalMilliseconds;
        totalSeconds      = ts.TotalSeconds;
    }
}
