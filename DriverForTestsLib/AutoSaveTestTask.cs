using System.Reflection;
using System.Text;

namespace DriverForTestsLib;


public class AutoSaveTestTask: TestTask
{
                                                                /// <summary>Директория для хранения файлов</summary>
    public DirectoryInfo dirForFiles { get; protected set; }    /// <summary>Путь для файла</summary>
    public string        path        { get; protected set; }

    /// <param name="name">Имя задачи (имя файла, должно быть уникально и содержать символы, допустимые для файлов)</param>
    /// <param name="dirForFiles">Директория для хранения файлов</param>
    /// <param name="executer_and_saver">Задача, которая будет выполняться</param>
    public AutoSaveTestTask(string name, DirectoryInfo dirForFiles, TaskResultSaver executer_and_saver): base(name)
    {
        this.dirForFiles = dirForFiles;
        this.path        = Path.Combine(dirForFiles.FullName, name);

        if (!dirForFiles.Exists)
            dirForFiles.Create();

        this.executer_and_saver = executer_and_saver;
        this._taskFunc = () =>
        {
            var result = this.executer_and_saver.ExecuteTest(this);
            this.executer_and_saver.Save(this, result);
        };
    }

    protected TestTaskFn _taskFunc;
    /// <summary>Функция тестирования, которая вызывается библиотекой. См. executer_and_saver </summary>
    public override TestTaskFn taskFunc
    {
        get
        {
            return _taskFunc;
        }
        protected set
        {
            if (_taskFunc is not null)
                throw new InvalidOperationException("The FirstSaveTestTask.taskFunc property is not accessible in the FirstSaveTestTask class");
        }
    }

    /// <summary>Устанавливается в конструкторе. Определяет задачу, которая будет вызываться</summary>
    public TaskResultSaver executer_and_saver;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
public class TaskResultSaverAttribute: Attribute
{}

public class AutoSaveTestError: TestError
{}

/// <summary>Класс, определяющий функцию, которая выполняет тестирование</summary>
public abstract class TaskResultSaver
{
    public virtual bool canCreateFile {get; set;} = false;

    /// <summary>Функция, вызываемая при выполнении тестирования</summary>
    /// <param name="task">FirstSaveTestTask, из которой вызывается данная функция</param>
    /// <returns>Возвращает объект для сохранения</returns>
    public abstract object ExecuteTest(AutoSaveTestTask task);

    /// <summary>Эта функция осуществляет сохранение для дальнейшего сравнения, само сравнение и регистрацию ошибок</summary>
    /// <param name="task">FirstSaveTestTask, из которой вызывается данная функция</param>
    /// <param name="result">Объект для сохранения</param>
    public virtual void Save(AutoSaveTestTask task, object result)
    {
        var fi = getSaveFileInfo(task, result);

        var text = getText(task, result);
        if (text is null)
            throw new Exception($"TaskResultSaver.Save.getText == null for task '{task.Name}'");

        if (fi.Exists)
        {
            if (!doCompare(fi, text))
            {
                var e = new AutoSaveTestError();
                e.Message = $"AutoSaveTestError: new string is not equal to string that was got from file '{fi.FullName}'";
                task.error.Add(e);
            }

            return;
        }
        else
        {
            if (canCreateFile)
            {
                var e = new AutoSaveTestError();
                e.Message = $"AutoSaveTestError: file '{fi.FullName}' not exists; created";
                task.error.Add(e);

                File.WriteAllText(fi.FullName, text);
            }
            else
            {
                var e = new AutoSaveTestError();
                e.Message = $"AutoSaveTestError: file '{fi.FullName}' not exists; file creation is restricted";
                task.error.Add(e);
            }
        }
    }

    public virtual FileInfo getSaveFileInfo(AutoSaveTestTask task, object result)
    {
        return new FileInfo(task.path);
    }

    public virtual bool doCompare(FileInfo fi, string text)
    {
        var textFromFile = File.ReadAllText(fi.FullName);

        return textFromFile == text;
    }

    public static bool haveTaskResultSaverAttribute(Type type)
    {
        return type.GetCustomAttributes(typeof(TaskResultSaverAttribute), true).Length > 0;
    }

    public static bool haveTaskResultSaverAttribute(MemberInfo type)
    {
        if (type.GetCustomAttributes(typeof(TaskResultSaverAttribute), true).Length > 0)
            return true;

        var f = type as FieldInfo;
        if (f is not null)
        if (haveTaskResultSaverAttribute(f.FieldType))
            return true;

        var p = type as PropertyInfo;
        if (p is not null)
        if (haveTaskResultSaverAttribute(p.PropertyType))
            return true;

        return false;
    }

    public virtual string? getText(AutoSaveTestTask task, object result, string separator = "7y8EX6fvtloAWsY7lANx5arDxLZROJ6H", TextFromFieldProcess? _tffp = null, int nesting = 0)
    {
        var tffp   = _tffp;
            tffp ??= new TextFromFieldProcess(task, separator, new StringBuilder());
        var sb     = tffp.sb;

        var rt       = result.GetType();
        var members  = rt.GetMembers();
        var allField = haveTaskResultSaverAttribute(rt);

        // Это, в общем-то, не нужно: на случай, если объекты в разных задачах случайно начнут работать одновременно (и то весь доступ на чтение идёт)
        // lock (result)
        foreach (var member in members)
        {
            if (!member.MemberType.HasFlag(MemberTypes.Field))
            if (!member.MemberType.HasFlag(MemberTypes.Property))
                continue;

            // Если над всем классом стоит атрибут - сохраняем все поля и свойства
            if (!allField)
            {
                // Если над свойством или полем стоит атрибут - его тоже сохраняем
                // Если сам тип поля/свойства с атрибутом - тоже сохраняем
                if (!haveTaskResultSaverAttribute(member))
                    continue;
            }

            sb.AppendLine
            (
                getTextFromField(member, result, tffp, nesting)
            );
        }

        if (_tffp is not null)
            return null;

        return sb.ToString();
    }

    public unsafe class TextFromFieldProcess
    {
        public AutoSaveTestTask task;
        public string           separator;
        public StringBuilder    sb;

        public readonly struct ListedObject
        {
            public ListedObject(object obj, Int64 number)
            {
                this.obj    = obj;
                this.number = number;
            }

            public readonly object obj;
            public readonly Int64  number;
        }

        public class ListOfObjects: List<ListedObject>
        {
            public ListOfObjects(int capacity): base(capacity)
            {}

            public ListedObject? ContainsKey(object obj)
            {
                foreach (var lob in this)
                {
                    if (lob.obj == obj)
                        return lob;
                }

                return null;
            }
        }

        public ListOfObjects listOfObjects = new ListOfObjects(128);
        public TextFromFieldProcess(AutoSaveTestTask task, string separator, StringBuilder sb)
        {
            this.task      = task;
            this.separator = separator;
            this.sb        = sb;
        }
    }

    public virtual string getTextFromField(System.Reflection.MemberInfo member, object source, TextFromFieldProcess tffp, int nesting)
    {
        System.Reflection.FieldInfo?    field = member as System.Reflection.FieldInfo;
        System.Reflection.PropertyInfo? prop  = member as System.Reflection.PropertyInfo;

        bool isField = field is not null ? true: false;
        bool isProp  = prop  is not null ? true: false;
        
        if (!isField && !isProp)
            throw new Exception("TaskResultSaver.getTextFromField: !isField && !isProp");

        var numberOfObject = tffp.listOfObjects.Count;
        var type = isField ? "field" : "property";
        var bstr = $"separator: number:{numberOfObject:D4}/{tffp.separator}/{nesting:D4}\n{type}: {member.Name}\n";
        var estr = $"\n----------------\nend separator: {numberOfObject:D4}/{tffp.separator}/{nesting:D4}\n{type}: {member.Name}\n";

        lock (tffp.listOfObjects)
        {
            var lob = tffp.listOfObjects.ContainsKey(source);
            if (lob is not null)
            {
                return bstr + $"\n{{already saved with number {lob.Value.number:D4} }}\n" + estr;
            }

            tffp.listOfObjects.Add(new TextFromFieldProcess.ListedObject(source, numberOfObject));
        }

        object? val;
        if (isField)
        {
            val = field?.GetValue(source);
        }
        else
        {
            val = prop?.GetValue(source);
        }

        var vstr = "";
        if (val is null)
        {
            vstr = "null";
        }
        else
        if (member.GetType().IsPrimitive)
        {
            vstr = val.ToString();
        }
        else
        {
            vstr = getText(tffp.task, val, tffp.separator, tffp, nesting + 1);
        }

        return bstr + vstr + estr;
    }
}
