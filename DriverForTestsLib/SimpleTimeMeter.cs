namespace DriverForTestsLib;

/// <summary>Помогает легко замерить время между двумя точками кода</summary>
public class SimpleTimeMeter : IDisposable
{
    public readonly DateTime start;
    public          DateTime End   {get; protected set;}
    public SimpleTimeMeter()
    {
        start = DateTime.Now;
        End   = default;
    }

    /// <summary>Заканчивает измерение времени. Можно вызывать повторно без изменения интервала времени</summary>
    public void Dispose()
    {
        if (End == default)
        {
            End = DateTime.Now;
            SetTimeSpan();
        }

        GC.SuppressFinalize(this);
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

    protected void SetTimeSpan()
    {
        if (ts != default)
            return;

        if (End == default)
            throw new NotEndedException();

        ts = (End - start);
        totalMilliseconds = ts.TotalMilliseconds;
        totalSeconds      = ts.TotalSeconds;
    }
}
