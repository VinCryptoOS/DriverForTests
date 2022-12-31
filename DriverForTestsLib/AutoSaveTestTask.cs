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
    public AutoSaveTestTask(string name, DirectoryInfo dirForFiles, TaskResultSaver executer_and_saver, TestConstructor constructor): base(name, constructor)
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
public class TaskResultSaver_DoNotSaveAttribute: Attribute
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

                using (File.Open(fi.FullName, FileMode.CreateNew))
                {}

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


    public class TextFromFieldProcess
    {
        public AutoSaveTestTask task;
        public string           separator;
        public long             objectCounter = 0;

        public class ListedObject
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
        public TextFromFieldProcess(AutoSaveTestTask task, string separator)
        {
            this.task      = task;
            this.separator = separator;
        }
    }

    public static bool haveTaskResultSaver_DoNotSaveAttribute(Type type)
    {
        return type.GetCustomAttributes(typeof(TaskResultSaver_DoNotSaveAttribute), true).Length > 0;
    }

    public static bool haveTaskResultSaver_DoNotSaveAttribute(MemberInfo type)
    {
        if (type.GetCustomAttributes(typeof(TaskResultSaver_DoNotSaveAttribute), true).Length > 0)
            return true;

        var f = type as FieldInfo;
        if (f is not null)
        if (haveTaskResultSaver_DoNotSaveAttribute(f.FieldType))
            return true;

        var p = type as PropertyInfo;
        if (p is not null)
        if (haveTaskResultSaver_DoNotSaveAttribute(p.PropertyType))
            return true;

        return false;
    }

    readonly struct IEnumerable_Object_Wrapper
    {
        public IEnumerable_Object_Wrapper(IEnumerable<object> @object)
        {
            this.enumerableObject = @object;
        }

        public readonly IEnumerable<object> enumerableObject;
    }

    public virtual string? getText(AutoSaveTestTask task, object? result, string separator = "7y8EX6fvtloAWsY7lANx5arDxLZROJ6H", TextFromFieldProcess? _tffp = null, int nesting = 0)
    {
        var tffp   = _tffp;
            tffp ??= new TextFromFieldProcess(task, separator);
        var sb     = new StringBuilder(128);

        if (result == null)
            return "null";

        if (_tffp == null && result is IEnumerable<object> obj)
        {
            return getText
            (
                task:      task,
                result:    new IEnumerable_Object_Wrapper(obj),
                separator: separator,
                _tffp:     tffp,
                nesting:   nesting
            );
        }

        if (isContainsOrRegisterNew(result, out TextFromFieldProcess.ListedObject? lob))
        {
            return $"\n{{already saved with number {lob?.number:D4} }}\n";
        }

        var rt       = result.GetType();
        var members  = rt.GetMembers();
        var allField = !haveTaskResultSaver_DoNotSaveAttribute(rt);

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
                // Если над свойством или полем стоит атрибут - его не сохраняем
                // Если сам тип поля/свойства с атрибутом - не сохраняем
                if (haveTaskResultSaver_DoNotSaveAttribute(member))
                    continue;
            }

            var text = getTextFromField(member, lob, tffp, nesting);

            if (text is not null)
                sb.AppendLine(text);
        }

        return sb.ToString();

        bool isContainsOrRegisterNew(object obj, out TextFromFieldProcess.ListedObject? lob)
        {
            lock (tffp.listOfObjects)
            {
                lob = tffp.listOfObjects.ContainsKey(obj);

                if (lob != null)
                    return true;

                lob = new TextFromFieldProcess.ListedObject(  obj: obj, number: Interlocked.Increment(ref tffp.objectCounter)  );
                tffp.listOfObjects.Add(lob);

                return false;
            }
        }
    }

    public static bool isElementaryType(Type type, object? obj)
    {
        if (type == null)
            return true;

        return     type.IsPrimitive || type.IsEnum
                || typeof(String).IsInstanceOfType(obj);
    }

    public virtual string? getTextFromField(System.Reflection.MemberInfo member, in TextFromFieldProcess.ListedObject? source, TextFromFieldProcess tffp, int nesting)
    {
        System.Reflection.FieldInfo?    field = member as System.Reflection.FieldInfo;
        System.Reflection.PropertyInfo? prop  = member as System.Reflection.PropertyInfo;

        bool isField = field is not null ? true: false;
        bool isProp  = prop  is not null ? true: false;
        
        if (!isField && !isProp)
            throw new Exception("TaskResultSaver.getTextFromField: !isField && !isProp");


        object? val;
        if (isField)
        {
            val = field?.GetValue(source?.obj);
        }
        else
        {
            if (prop?.GetIndexParameters().Length > 0)
                return null;

            val = prop?.GetValue(source?.obj);
        }

        var mType = val?.GetType();
        // var type = isField ? "field" : "property";
        var bstr = $"\n----------------\nseparator: number:{source?.number:D4}/{tffp.separator}/{nesting:D4}\n{member.Name}: {mType?.FullName}\t\tFrom type {source?.obj.GetType().FullName}\n";
        var estr = $"\nend separator: {source?.number:D4}/{tffp.separator}/{nesting:D4}\t{member.Name}\n----------------\n\n";

        var vstr = "";
        if (val is null || mType == null)
        {
            vstr = "null";
        }
        else
        if (  TaskResultSaver.isElementaryType(mType, val)  )
        {
            vstr = val.ToString();
        }
        else
        {
            vstr = getText(tffp.task, val, tffp.separator, tffp, nesting + 1);
        }

        if (val is IEnumerable<object> vals)
        {
            var sba = new StringBuilder(128);
            sba.AppendLine("values:");

            int cnt = 0;
            foreach (var v in vals)
            {
                if (TaskResultSaver.isElementaryType(v.GetType(), v))
                    sba.AppendLine($"{cnt:D4}:" + v.ToString());
                else
                    sba.AppendLine($"{cnt:D4}:" + getText(tffp.task, v, tffp.separator, tffp, nesting + 1));

                cnt++;
            }
            sba.AppendLine("end values");

            if (vstr != null && vstr.Length > 0)
                vstr += "\n\n" + sba.ToString();
            else
                vstr += sba.ToString();
        }

        return bstr + vstr + estr;
    }
}
