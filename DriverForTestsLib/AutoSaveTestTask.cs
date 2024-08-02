using System.Reflection;
using System.Text;

namespace DriverForTestsLib;


public class AutoSaveTestTask: TestTask
{
    #nullable disable
    protected DirectoryInfo _dirForFiles;
                                                                /// <summary>Директория для хранения файлов</summary>
    public DirectoryInfo DirForFiles
    {
        get => _dirForFiles;
        protected set
        {
            this._dirForFiles = value;
            this.Path         = System.IO.Path.Combine(DirForFiles.FullName, base.Name);
        }
    }

    public string _path;                                            /// <summary>Путь для файла</summary>
    public string  Path
    {
        get => _path;
        protected set
        {
            // Имя файла в Linux не должно заканчиваться на пробельные символы
            if (value.EndsWith(" "))
                value = value[0 .. ^1] + ".";

            _path = value;
        }
    }

    #nullable restore

    /// <param name="name">Имя задачи (имя файла, должно быть уникально и содержать символы, допустимые для файлов)</param>
    /// <param name="dirForFiles">Директория для хранения файлов</param>
    /// <param name="executer_and_saver">Задача, которая будет выполняться</param>
    /// <param name="constructor">Конструктор задач, который создаёт эту задачу</param>
    public AutoSaveTestTask(string name, DirectoryInfo dirForFiles, TaskResultSaver executer_and_saver, TestConstructor constructor): base(name, constructor)
    {
        this.DirForFiles = dirForFiles;

        this.executer_and_saver = executer_and_saver;
        this._taskFunc = () =>
        {
            // this - т.к. dirForFiles может измениться к моменту запуска тестов
            if (this.executer_and_saver.CanCreateFile)
            if (!this.DirForFiles.Exists)
                this.DirForFiles.Create();

            var result = this.executer_and_saver.ExecuteTest(this);
            this.executer_and_saver.Save(this, result);
        };
    }

    protected TestTaskFn _taskFunc;
    /// <summary>Функция тестирования, которая вызывается библиотекой. См. executer_and_saver </summary>
    public override TestTaskFn TaskFunc
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
    public virtual bool CanCreateFile {get; set;} = false;

    /// <summary>Функция, вызываемая при выполнении тестирования</summary>
    /// <param name="task">FirstSaveTestTask, из которой вызывается данная функция</param>
    /// <returns>Возвращает объект для сохранения</returns>
    public abstract object ExecuteTest(AutoSaveTestTask task);

    /// <summary>Эта функция осуществляет сохранение для дальнейшего сравнения, само сравнение и регистрацию ошибок</summary>
    /// <param name="task">FirstSaveTestTask, из которой вызывается данная функция</param>
    /// <param name="result">Объект для сохранения</param>
    public virtual void Save(AutoSaveTestTask task, object result)
    {
        var fi = GetSaveFileInfo(task, result);

        // Все сохранения должны быть в идентичной культуре
        var cc = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            var text = GetText(task, result)
                ?? throw new Exception($"TaskResultSaver.Save.getText == null for task '{task.Name}'");

            fi.Refresh();
            if (fi.Exists)
            {
                if (!DoCompare(fi, text))
                {
                    var e = new AutoSaveTestError();
                    e.Message = $"AutoSaveTestError: new string is not equal to string that was got from file '{fi.FullName}'";
                    task.error.Add(e);

                    File.WriteAllText(fi.FullName + ".error", text);
                }

                return;
            }
            else
            {
                if (CanCreateFile)
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
                    e.Message = $"AutoSaveTestError: file '{fi.FullName}' not exists; file creation is restricted by canCreateFile flag";
                    task.error.Add(e);
                }
            }

        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = cc;
        }
    }

    public virtual FileInfo GetSaveFileInfo(AutoSaveTestTask task, object result)
    {
        return new FileInfo(task.Path);
    }

    public virtual bool DoCompare(FileInfo fi, string text)
    {
        var textFromFile = File.ReadAllText(fi.FullName);

        return textFromFile == text;
    }


    public class TextFromFieldProcess
    {
        public AutoSaveTestTask task;
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

        public ListOfObjects listOfObjects = new(128);
        public TextFromFieldProcess(AutoSaveTestTask task)
        {
            this.task      = task;
        }
    }

    public static bool HaveTaskResultSaver_DoNotSaveAttribute(Type type)
    {
        return type.GetCustomAttributes(typeof(TaskResultSaver_DoNotSaveAttribute), true).Length > 0;
    }

    public static bool HaveTaskResultSaver_DoNotSaveAttribute(MemberInfo type)
    {
        if (type.GetCustomAttributes(typeof(TaskResultSaver_DoNotSaveAttribute), true).Length > 0)
            return true;

        var f = type as FieldInfo;
        if (f is not null)
        if (HaveTaskResultSaver_DoNotSaveAttribute(f.FieldType))
            return true;

        var p = type as PropertyInfo;
        if (p is not null)
        if (HaveTaskResultSaver_DoNotSaveAttribute(p.PropertyType))
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

    protected SortedList<int, string> indentationStrings = new();
    public string GetIndent(int nesting)
    {
        if (indentationStrings.ContainsKey(nesting))
            return indentationStrings[nesting];

        var sb = new StringBuilder(nesting);
        sb.Append('\t', nesting);

        var val = sb.ToString();
        indentationStrings.Add(nesting, val);

        return val;
    }

    public string AddIndentation(int nesting, string? stringToChange, bool addToStart = true)
    {
        if (stringToChange == null)
            return "null" + GetIndent(nesting);

        var replaced = stringToChange.Replace("\n", "\n" + GetIndent(nesting));

        if (addToStart)
            return GetIndent(nesting) + replaced;

        return replaced;
    }

    public virtual string? GetText(AutoSaveTestTask task, object? result, string FullName = "", TextFromFieldProcess? _tffp = null, int nesting = 0)
    {
        var tffp   = _tffp;
            tffp ??= new TextFromFieldProcess(task);
        var sb     = new StringBuilder(128);

        if (result == null)
            return AddIndentation(nesting, "null");

        if (_tffp == null && result is byte[] bytes)
        {
            return GetTextForByteArray(nesting, bytes);
        }

        if (_tffp == null && result is IEnumerable<object> obj)
        {
            return GetText
            (
                task:      task,
                result:    new IEnumerable_Object_Wrapper(obj),
                FullName:  FullName,
                _tffp:     tffp,
                nesting:   nesting
            );
        }

        if (isContainsOrRegisterNew(result, out TextFromFieldProcess.ListedObject? lob))
        {
            return AddIndentation
            (
              nesting:        nesting + 1,
              stringToChange: $"\n{{already saved with number {lob?.number:D4}{"}"}\n"
            );
        }

        sb.AppendLine
        (
            AddIndentation
            (
                nesting:        nesting,
                stringToChange: $"\n{{object number {lob?.number:D4}{"}"}"
            )
        );

        if (result is byte[] bytes2)
        {
            return sb.ToString() + GetTextForByteArray(nesting, bytes2);
        }

        var rt       = result.GetType();
        var members  = rt.GetMembers();
        var allField = !HaveTaskResultSaver_DoNotSaveAttribute(rt);

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
                if (HaveTaskResultSaver_DoNotSaveAttribute(member))
                    continue;
            }

            var text = GetTextFromField(member, lob, tffp, nesting + 1, FullName + "." + member.Name);

            if (text is not null)
                sb.AppendLine(text);
        }

        if (result is System.Collections.Generic.IEnumerable<object> || result is System.Collections.IEnumerable)
        {
            var text = GetTextFromField(null, lob, tffp, nesting + 1, FullName + "[IEnumerable]");

            if (text is not null)
                sb.AppendLine(text);
        }

        sb.Append
        (
            AddIndentation
            (
                nesting: nesting,
                stringToChange: $"\nend of {"{"}{lob?.number:D4}{"}"} {FullName}",
                false
            )
        );

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

    protected static string AddSpacesToHexString(string str)
    {
        var sb  = new StringBuilder();
        int i = 0;
        for (; i < str.Length - 4; i += 4)
        {
            sb.Append(  str[i .. (i+4)]  );
            sb.Append(' ');

            if ((i % hexMax) == hexMax - 4)
                sb.Append("  ");
        }
        if (i < str.Length)
            sb.Append(str[i .. ]);

        return sb.ToString().TrimEnd();
    }

    protected const int hexMax  = 2 << 3;   // 2 << 3 == 4
    protected const int hexMax2 = hexMax  * 2;
    protected const int hexMax4 = hexMax2 * 2;
    protected string GetTextForByteArray(int nesting, byte[] bytes)
    {
        var str = Convert.ToHexString(bytes);
        var sb  = new StringBuilder();
        int i = 0;
        for (; i < str.Length - hexMax4 - 1; i += hexMax2)
        {
            var s = AddSpacesToHexString(    str[ i .. (i+hexMax2) ]    );
            sb.Append("\n" + s);
        }

        if (i < str.Length)
        {
            var ns = AddSpacesToHexString(  str[i .. ]  );
            sb.Append("\n" + ns);
        }

        return AddIndentation
                (
                    nesting,
                    $"byte[{bytes.LongLength}]:"
                )
            +
                AddIndentation
                (
                    nesting + 1,
                    sb.ToString(),
                    false
                );
    }

    public static bool IsElementaryType(Type type, object? obj)
    {
        if (type == null)
            return true;

        return     type.IsPrimitive || type.IsEnum
                || typeof(String).IsInstanceOfType(obj);
    }

    public virtual string? GetTextFromField(System.Reflection.MemberInfo? member, in TextFromFieldProcess.ListedObject? source, TextFromFieldProcess tffp, int nesting, string FullName)
    {
        System.Reflection.FieldInfo?    field = member as System.Reflection.FieldInfo;
        System.Reflection.PropertyInfo? prop  = member as System.Reflection.PropertyInfo;

        bool isField = field is not null;
        bool isProp  = prop  is not null;
        
        /*if (!isField && !isProp)
            throw new Exception("TaskResultSaver.getTextFromField: !isField && !isProp");
            */

        object? val;
        if (isField)
        {
            val = field?.GetValue(source?.obj);
        }
        else
        if (isProp)
        {
            if (prop?.GetIndexParameters().Length > 0)
                return null;

            val = prop?.GetValue(source?.obj);
        }
        else
            val = source?.obj;  // Это массивы и другие IEnumerator без параметрического полиморфизма

        var mType = val?.GetType();
        // var type = isField ? "field" : "property";
//        var bstr = $"\n----------------\nseparator: number:{source?.number:D4}/{tffp.separator}/{nesting:D4}\n{member.Name}: {mType?.FullName}\t\tFrom type {source?.obj.GetType().FullName}\n";
//         var estr = $"\nend separator: {source?.number:D4}/{tffp.separator}/{nesting:D4}\t{member.Name}\n----------------\n\n";

        var bstr = AddIndentation
            (
              nesting:        nesting,
              stringToChange: $"\n{member?.Name ?? "[IEnumerable]"}:\t\t{mType?.FullName}\t\t{FullName}"
            );
        var estr = "";

        if (val is byte[] bytes)
        {
            return GetText(tffp.task, bytes, FullName, tffp, nesting);
        }

        var  vstr = "";
        bool isEnumerable = val is IEnumerable<object> || val is System.Collections.IEnumerable;
        if (val is null || mType == null)
        {
            vstr = AddIndentation(nesting+1, "\nnull", false);
        }
        else
        if (  TaskResultSaver.IsElementaryType(mType, val)  )
        {
            vstr = AddIndentation(nesting+1, "\n" + val.ToString(), false);
        }
        else
        if (isEnumerable && member == null)
        {
            var sba  = new StringBuilder(128);
            sba.Append("\n" + GetIndent(nesting+1) + "[values:]\n");

            int   cnt   = 0;
            Type? pType = null;
            if (val is IEnumerable<object> vals)
            {
                foreach (var v in vals)
                {
                    cnt = GetTextForArrayVal(tffp, nesting, FullName, sba, cnt, v, ref pType);
                }
            }
            else
            if (val is System.Collections.IEnumerable valsA)
            {
                foreach (var v in valsA)
                {
                    cnt = GetTextForArrayVal(tffp, nesting, FullName, sba, cnt, v, ref pType);
                }
            }
            sba.AppendLine(GetIndent(nesting+1) + "[end values]");

            if (vstr != null && vstr.Length > 0)
                vstr += "\n\n" + sba.ToString();
            else
                vstr += sba.ToString();
        }
        else
        if (member != null && source != null)
        {
            GetMemberText(source, tffp, nesting, FullName, val, out estr, out vstr, source.number);
        }
        else
            throw new Exception();

        return bstr + vstr + estr;
    }

    private void GetMemberText(TextFromFieldProcess.ListedObject? source, TextFromFieldProcess tffp, int nesting, string FullName, object? val, out string estr, out string? vstr, long objectNumber)
    {
        vstr = GetText(tffp.task, val, FullName, tffp, nesting);

        estr = "";
    }

    protected int GetTextForArrayVal(TextFromFieldProcess tffp, int nesting, string FullName, StringBuilder sba, int cnt, object v, ref Type? prevType)
    {
        var vt      = v.GetType();
        var typeStr = "";
        if (prevType != vt)
        {
            typeStr  = $"\t\t[:{vt.Name}]";
            prevType = vt;
        }

        var cntStr = $"{cnt:D2}:";
        var number = GetIndent(nesting + 2) + $"{cntStr, -8}";

        if (TaskResultSaver.IsElementaryType(vt, v))
            sba.AppendLine(number + v.ToString() + typeStr);
        else
            sba.AppendLine(number + typeStr + GetText(tffp.task, v, FullName + $"[{cnt:D4}]", tffp, nesting + 2));

        cnt++;
        return cnt;
    }
}
