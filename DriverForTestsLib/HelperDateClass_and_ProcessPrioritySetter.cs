using System.Diagnostics;

// TODO: что у нас насчёт проверки целостности программы?
namespace DriverForTestsLib;


// -------------------------------- ProcessPrioritySetter --------------------------------

/// <summary>Задаёт процессу приоритет выполнения, а после использования возвращает его назад</summary>
/// <remarks>Использовать с using
/// <para>
/// using var processPrioritySetter = new ProcessPrioritySetter(tests.ProcessPriority, true)
/// </para></remarks>
public class ProcessPrioritySetter: IDisposable
{                                                                   /// <summary>Устанавливаемый приоритет процесса</summary>
    public ProcessPriorityClass? ProcessPriority;                   /// <summary>Изначальный приоритет процесса</summary>
    public ProcessPriorityClass? initialProcessPriority;

    /// <summary>Устанавливает приоритет процесса, запоминая старый приоритет</summary>
    /// <param name="ProcessPriority">Новый приоритет процесса</param>
    /// <param name="ExceptionToConsole">Если true, то при возникновении исключения информация о нём будет выведена на консоль</param>
    /// <param name="ConstructorException">Обработчик исключения в конструкторе</param>
    /// <param name="DestructorException" >Обработчик исключения в деструкторе</param>
    public ProcessPrioritySetter(ProcessPriorityClass? ProcessPriority, bool ExceptionToConsole = true, ExceptionInConstructorEvent? ConstructorException = null, ExceptionInDestructorEvent? DestructorException = null)
    {
        SetExceptionHandlers(ExceptionToConsole, ref ConstructorException, ref DestructorException);

        if (ProcessPriority.HasValue)
            try
            {
                var cp = Process.GetCurrentProcess();

                initialProcessPriority = cp.PriorityClass;
                this.ProcessPriority = ProcessPriority;

                cp.PriorityClass = ProcessPriority.Value;
            }
            catch (Exception e)
            {
                var cancel = false;
                this.ConstructorException?.Invoke(e, ref cancel);

                if (cancel)
                {
                    this.Disposing();
                }
            }
    }

    /// <summary>Регистрирует обработчики исключений в данном классе. Параметры те же, что и в конструкторе</summary>
    protected virtual void SetExceptionHandlers(bool ExceptionToConsole, ref ExceptionInConstructorEvent? ConstructorException, ref ExceptionInDestructorEvent? DestructorException)
    {
        if (ExceptionToConsole)
        {
            this.ConstructorException += OutputErrorToConsole;
            this.DestructorException  += OutputErrorToConsole;
        }

        if (ConstructorException is not null)
            this.ConstructorException += ConstructorException;

        if (DestructorException is not null)
            this.DestructorException  += DestructorException;
    }

    /// <summary>Повторно устанавливает PriorityClass из значения ProcessPriority</summary>
    public virtual void SetPriority()
    {
        if (ProcessPriority.HasValue)
        Process.GetCurrentProcess().PriorityClass = ProcessPriority.Value;
    }

    /// <summary>Возвращает приоритет процессу (если может) и снимает с регистрации обработчики событий</summary>
    public virtual void Disposing()
    {
        try
        {
            if (!initialProcessPriority.HasValue)
                return;

            var cp = Process.GetCurrentProcess();
            cp.PriorityClass = initialProcessPriority.Value;     // Восстанавливаем приоритет процесса, который был до этого
        }
        // Под Linux повышение приоритета процесса может быть запрещено. В таком случае будет выдана ошибка
        catch (Exception e)
        {
            DestructorException?.Invoke(e);
        }
        finally
        {
            this.ConstructorException = null;
            this.DestructorException  = null;
        }
    }

    void IDisposable.Dispose()
    {
        Disposing();
        GC.SuppressFinalize(this);
    }

    public delegate void ExceptionInConstructorEvent  (Exception e, ref bool doCancelWorkInClass);
    public delegate void ExceptionInDestructorEvent   (Exception e);
    public event         ExceptionInConstructorEvent? ConstructorException;
    public event         ExceptionInDestructorEvent?  DestructorException;

    /// <summary>Выводит исключение на консоль (stderr)</summary><param name="e">Выводимое исключение</param>
    public virtual void OutputErrorToConsole(Exception e)
    {
        // Под Linux повышение приоритета процесса может быть запрещено. В таком случае будет выдана ошибка
        // Смотрим на код и не выводим ошибку
        if (e.Message == "Permission denied")
            return;

        Console.Error.WriteLine($"ProcessPrioritySetter error: Failed to restore process priority: {e.Message}\n{e.StackTrace}\n\n");
    }

    /// <summary>Выводит исключение на консоль (stderr)</summary><param name="e">Выводимое исключение</param><param name="doCancelWorkInClass">Если вернуть true, то конструктор удалит объект</param>
    public virtual void OutputErrorToConsole(Exception e, ref bool doCancelWorkInClass)
    {
        Console.Error.WriteLine($"ProcessPrioritySetter error: Failed to set process priority: {e.Message}\n{e.StackTrace}\n\n");
    }
}


// -------------------------------- HelperClass --------------------------------


/// <summary>Вспомогательный класс для вывода форматированной даты и времени</summary>
public static class HelperDateClass
{
    public static string DateToDateString(DateTime now)
    {
        return
            now.Year.ToString("D4") + "." + now.Month.ToString("D2") + "." + now.Day.ToString("D2") + " " +
            now.Hour.ToString("D2") + ":" + now.Minute.ToString("D2") + ":" + now.Second.ToString("D2") + "." + now.Millisecond.ToString("D3");
    }

    public static string DateToDateFileString(DateTime now)
    {
        return
            now.Year.ToString("D4") + "-" + now.Month.ToString("D2") + now.Day.ToString("D2") + "-" +
            now.Hour.ToString("D2") + now.Minute.ToString("D2") + "-" + now.Second.ToString("D2") + "" + now.Millisecond.ToString("D3");
    }

    public static string TimeStampTo_HHMMSS_String(TimeSpan span)
    {
        return span.ToString(@"hh\'mm\:ss");
    }

    public static string TimeStampTo_HHMMSSfff_String(TimeSpan span)
    {
        return span.ToString(@"hh\'mm\:ss\.fff");
    }
}
