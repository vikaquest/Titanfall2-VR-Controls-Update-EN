using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Reflection;

[assembly: AssemblyTitle("Titanfall 2 VR — Апдейт управления")]
[assembly: AssemblyVersion("1.5.0.0")]
[assembly: AssemblyFileVersion("1.5.0.0")]
[assembly: AssemblyInformationalVersion("1.5.0")]

public class InstallFile { public string path,data,hash; public string[] accepted; public bool required; }
public class Requirement { public string path,hash; }
public class Feature { public string id,label; public int bit; }
public class Payload { public string version,backupFolder; public Feature[] features; public InstallFile[] files; public Requirement[] requirements; }
public class InstallState { public int format=1; public string release="1.5.0"; public Dictionary<string,string> initial,current; }
public class Engine {
    public static JavaScriptSerializer Json=new JavaScriptSerializer { MaxJsonLength=64000000 };
    public Payload P;
    public Engine(){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.json"))using(var r=new StreamReader(s,Encoding.UTF8))P=Json.Deserialize<Payload>(r.ReadToEnd());}
    public static string Hash(byte[] b){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
    string PathAt(string root,string rel){
        string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        string full=Path.GetFullPath(Path.Combine(prefix,rel.Replace('/',Path.DirectorySeparatorChar)));
        if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new Exception("Недопустимый путь.");
        for(string t=full;t!=null;t=Path.GetDirectoryName(t))if((File.Exists(t)||Directory.Exists(t))&&(File.GetAttributes(t)&FileAttributes.ReparsePoint)!=0)throw new Exception("Путь содержит ссылку или junction: "+t);
        return full;
    }
    string BackupRel(string rel){return P.backupFolder+"/"+rel;}
    public string Backup(string root){return PathAt(root,P.backupFolder);}
    string FileHash(string path){return File.Exists(path)?Hash(File.ReadAllBytes(path)):null;}
    byte[] Data(InstallFile f){
        if(f.data==null)return null;
        using(var input=new MemoryStream(Convert.FromBase64String(f.data)))using(var gz=new System.IO.Compression.GZipStream(input,System.IO.Compression.CompressionMode.Decompress))using(var output=new MemoryStream()){
            gz.CopyTo(output);var bytes=output.ToArray();if(Hash(bytes)!=f.hash)throw new Exception("Повреждён встроенный файл: "+f.path);return bytes;
        }
    }
    InstallState State(string root){
        string path=PathAt(root,BackupRel("state.json"));if(!File.Exists(path))return null;
        var s=Json.Deserialize<InstallState>(File.ReadAllText(path));
        if(s==null||s.format!=1||s.release!="1.5.0"||s.initial==null||s.current==null||s.current.Count!=P.files.Length||s.initial.Count!=P.files.Length)throw new Exception("Повреждён журнал патча 1.5.0.");return s;
    }
    public void Check(string root){
        if(Directory.Exists(PathAt(root,BackupRel("pending"))))throw new Exception("Незавершённая установка. Нажмите «Восстановить».");
        foreach(var req in P.requirements)if(FileHash(PathAt(root,req.path))!=req.hash)throw new Exception("Неподдерживаемая версия игрового файла: "+req.path);
        var s=State(root);
        foreach(var f in P.files){
            string h=FileHash(PathAt(root,f.path));Data(f);
            if(s!=null){string expected;if(!s.current.TryGetValue(f.path,out expected)||h!=expected)throw new Exception("Файл изменён после установки патча: "+f.path);}
            else if(h==null?f.required:!(f.accepted??new string[0]).Contains(h))throw new Exception("Неизвестный или отсутствующий файл VR-мода: "+f.path+"\nТребуется проверенная версия VR-мода 1.0.17.");
        }
    }
    void ClosedGame(){foreach(var p in Process.GetProcesses())try{if(new[]{"titanfall2","titanfall2_trial","northstar","titanfall2vr"}.Contains(p.ProcessName.ToLowerInvariant()))throw new Exception("Закройте игру перед изменением файлов.");}finally{p.Dispose();}}
    static void Atomic(string path,byte[] bytes){
        Directory.CreateDirectory(Path.GetDirectoryName(path));if(bytes==null){if(File.Exists(path))File.Delete(path);return;}
        string tmp=path+".quest-"+Guid.NewGuid().ToString("N")+".tmp";
        try{using(var stream=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}finally{if(File.Exists(tmp))File.Delete(tmp);}
    }
    Dictionary<string,byte[]> Output(string root){var output=P.files.ToDictionary(f=>f.path,f=>Data(f));return output;}
    void Transaction(string root,Dictionary<string,byte[]> output){
        string pending=PathAt(root,BackupRel("pending"));Directory.CreateDirectory(pending);
        var previous=output.ToDictionary(x=>x.Key,x=>File.Exists(PathAt(root,x.Key))?Convert.ToBase64String(File.ReadAllBytes(PathAt(root,x.Key))):null);
        Atomic(Path.Combine(pending,"journal.json"),Encoding.UTF8.GetBytes(Json.Serialize(previous)));
        try{foreach(var pair in output){string path=PathAt(root,pair.Key);Atomic(path,pair.Value);if(FileHash(path)!=(pair.Value==null?null:Hash(pair.Value)))throw new Exception("Ошибка проверки записанного файла: "+pair.Key);}Directory.Delete(pending,true);}catch{Recover(root);throw;}
    }
    void Recover(string root){
        string pending=PathAt(root,BackupRel("pending"));string journal=Path.Combine(pending,"journal.json");
        if(!File.Exists(journal))throw new Exception("Нет журнала незавершённой установки.");
        var previous=Json.Deserialize<Dictionary<string,string>>(File.ReadAllText(journal));
        var allowed=new HashSet<string>(P.files.Select(f=>f.path));allowed.Add(BackupRel("state.json"));
        if(previous.Count!=allowed.Count||previous.Keys.Any(k=>!allowed.Contains(k)))throw new Exception("Некорректный журнал восстановления.");
        foreach(var pair in previous)Atomic(PathAt(root,pair.Key),pair.Value==null?null:Convert.FromBase64String(pair.Value));Directory.Delete(pending,true);
    }
    public void Apply(string root,int ignored){
        ClosedGame();Check(root);Directory.CreateDirectory(Backup(root));
        using(var gate=new FileStream(PathAt(root,BackupRel("lock")),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
            Check(root);var s=State(root);
            if(s==null){s=new InstallState { initial=P.files.ToDictionary(f=>f.path,f=>FileHash(PathAt(root,f.path))) };foreach(var f in P.files){
                string path=PathAt(root,BackupRel("initial/"+f.path));string expected=s.initial[f.path];
                if(File.Exists(path)&&FileHash(path)!=expected)throw new Exception("Конфликт предыдущей резервной копии: "+f.path);
                if(expected!=null&&!File.Exists(path))Atomic(path,File.ReadAllBytes(PathAt(root,f.path)));
            }}else foreach(var f in P.files)if(s.initial[f.path]!=null&&FileHash(PathAt(root,BackupRel("initial/"+f.path)))!=s.initial[f.path])throw new Exception("Повреждена резервная копия: "+f.path);
            var output=Output(root);s.current=output.ToDictionary(x=>x.Key,x=>x.Value==null?null:Hash(x.Value));output[BackupRel("state.json")]=Encoding.UTF8.GetBytes(Json.Serialize(s));Transaction(root,output);
        }
    }
    public void Restore(string root){
        ClosedGame();if(!Directory.Exists(Backup(root)))throw new Exception("Резервных копий патча 1.5.0 ещё нет.");
        using(var gate=new FileStream(PathAt(root,BackupRel("lock")),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
            if(Directory.Exists(PathAt(root,BackupRel("pending")))){Recover(root);return;}
            Check(root);var s=State(root);if(s==null)throw new Exception("Нет журнала установленного патча 1.5.0.");
            var output=new Dictionary<string,byte[]>();foreach(var f in P.files){byte[] b=s.initial[f.path]==null?null:File.ReadAllBytes(PathAt(root,BackupRel("initial/"+f.path)));if((b==null?null:Hash(b))!=s.initial[f.path])throw new Exception("Повреждена исходная копия: "+f.path);output[f.path]=b;}
            s.current=new Dictionary<string,string>(s.initial);output[BackupRel("state.json")]=Encoding.UTF8.GetBytes(Json.Serialize(s));Transaction(root,output);
        }
    }
}

public class FeatureCopy { public string id,group,groupEn,title,titleEn,description,descriptionEn,note,noteEn; public string[] controls,controlsEn; }
public static class Words {
#if EN
    public static readonly bool English=true;
#else
    public static readonly bool English=false;
#endif
    public static string T(string ru,string en){return English?en:ru;}
    public static string Error(string message){
        if(!English)return message;
        foreach(var pair in Errors.OrderByDescending(x=>x.Key.Length))message=message.Replace(pair.Key,pair.Value);
        return message;
    }
    public static Dictionary<string,string> Errors=new Dictionary<string,string>();
}
public static class Theme {
    public static Color Background=Color.FromArgb(17,23,29),Card=Color.FromArgb(25,34,43),Text=Color.FromArgb(229,237,243),Muted=Color.FromArgb(168,184,199),Border=Color.FromArgb(52,69,85),Accent=Color.FromArgb(104,188,232),Selected=Color.FromArgb(32,57,73);
    public static System.Drawing.Drawing2D.GraphicsPath Round(RectangleF r,float radius){var p=new System.Drawing.Drawing2D.GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
}
public class DarkCard:Panel {
    public Color Stroke=Theme.Border;
    public DarkCard(){DoubleBuffered=true;BackColor=Theme.Card;}
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var p=Theme.Round(new RectangleF(0.5f,0.5f,Width-1,Height-1),7))using(var brush=new SolidBrush(BackColor))using(var pen=new Pen(Stroke)){e.Graphics.FillPath(brush,p);e.Graphics.DrawPath(pen,p);}base.OnPaint(e);}
}
public class FeatureRow:DarkCard {
    public Label Caption=new Label(); public Feature Feature; public FeatureCopy Copy;
    public bool Selected; public event EventHandler Pick;
    public FeatureRow(Feature f,FeatureCopy copy){
        Feature=f;Copy=copy;Height=40;Margin=new Padding(0,0,0,3);BackColor=Theme.Card;
        Caption.Text=Words.T(copy.title,copy.titleEn);Caption.SetBounds(13,1,327,38);Caption.TextAlign=ContentAlignment.MiddleLeft;Caption.Font=new Font("Segoe UI",9.5f);Caption.ForeColor=Theme.Text;Caption.BackColor=Color.Transparent;Caption.Cursor=Cursors.Hand;Controls.Add(Caption);
        Caption.Click+=(s,e)=>{if(Pick!=null)Pick(this,EventArgs.Empty);};Click+=(s,e)=>{if(Pick!=null)Pick(this,EventArgs.Empty);};Enter+=(s,e)=>{if(Pick!=null)Pick(this,EventArgs.Empty);};
    }
    protected override void OnPaint(PaintEventArgs e){Stroke=Selected?Theme.Accent:Theme.Border;base.OnPaint(e);}
}
public class MainWindow:Form {
    Engine engine=new Engine(); TextBox folder=new TextBox(); Panel list=new Panel(); DarkCard detail=new DarkCard(); Label status=new Label(),count=new Label(),heading=new Label(),description=new Label(),conditions=new Label(),section=new Label(); FlowLayoutPanel bindings=new FlowLayoutPanel();
    Button apply=new Button(),restore=new Button(); List<FeatureRow> rows=new List<FeatureRow>(); List<FeatureCopy> copies; string selectedId;
    Color ink=Theme.Text,muted=Theme.Muted,accent=Theme.Accent,line=Theme.Border;
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attr,ref int value,int size);
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);try{int enabled=1;DwmSetWindowAttribute(Handle,20,ref enabled,4);}catch{}}
    public MainWindow(){
        using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("features.json"))using(var r=new StreamReader(s,Encoding.UTF8))copies=Engine.Json.Deserialize<List<FeatureCopy>>(r.ReadToEnd());
        using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("messages.json"))using(var r=new StreamReader(s,Encoding.UTF8))Words.Errors=Engine.Json.Deserialize<Dictionary<string,string>>(r.ReadToEnd());
        Text=Words.T("Titanfall 2 VR — Апдейт управления v1.5.0","Titanfall 2 VR — Controls Update v1.5.0");ClientSize=new Size(1120,888);MinimumSize=MaximumSize=Size;FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);BackColor=Theme.Background;ForeColor=ink;AutoScaleMode=AutoScaleMode.None;
        var title=new Label{Text="Titanfall 2 VR",Font=new Font("Segoe UI",24,FontStyle.Bold),Location=new Point(28,17),Size=new Size(600,45),ForeColor=ink};Controls.Add(title);
        Controls.Add(new Label{Text=Words.T("Апдейт управления 1.5.0  /  Quest & Touch","Controls Update 1.5.0  /  Quest & Touch"),Location=new Point(31,66),Size=new Size(790,24),ForeColor=muted});
        folder.BackColor=Theme.Card;folder.ForeColor=Theme.Text;folder.BorderStyle=BorderStyle.FixedSingle;folder.SetBounds(31,103,920,28);folder.Text=Directory.Exists(@"D:\SteamLibrary\steamapps\common\Titanfall2")?@"D:\SteamLibrary\steamapps\common\Titanfall2":"";Controls.Add(folder);
        var browse=MakeButton(Words.T("Папка игры…","Game folder…"),962,100,127,false);browse.Click+=(s,e)=>{using(var d=new FolderBrowserDialog{Description=Words.T("Выберите папку Titanfall2","Select your Titanfall2 folder"),SelectedPath=folder.Text})if(d.ShowDialog()==DialogResult.OK){folder.Text=d.SelectedPath;Inspect();}};
        Controls.Add(new Label{Text=Words.T("ВОЗМОЖНОСТИ","FEATURES"),Location=new Point(31,150),Size=new Size(353,22),ForeColor=muted,Font=new Font("Segoe UI",9,FontStyle.Bold)});
        list.SetBounds(31,180,366,565);list.AutoScroll=true;Controls.Add(list);
        detail.SetBounds(415,150,674,595);detail.BackColor=Theme.Card;Controls.Add(detail);
        section.SetBounds(27,23,614,24);section.ForeColor=accent;section.Font=new Font("Segoe UI",10,FontStyle.Bold);detail.Controls.Add(section);
        heading.SetBounds(27,59,615,96);heading.Font=new Font("Segoe UI",20,FontStyle.Bold);detail.Controls.Add(heading);
        description.SetBounds(29,163,610,86);description.Font=new Font("Segoe UI",12);description.ForeColor=muted;detail.Controls.Add(description);
        bindings.SetBounds(28,257,614,200);bindings.FlowDirection=FlowDirection.TopDown;bindings.WrapContents=false;bindings.AutoScroll=true;detail.Controls.Add(bindings);
        conditions.SetBounds(29,472,612,100);conditions.Font=new Font("Segoe UI",10);conditions.ForeColor=muted;detail.Controls.Add(conditions);
        count.SetBounds(31,761,700,23);count.Font=new Font("Segoe UI",10,FontStyle.Bold);Controls.Add(count);
        status.SetBounds(31,789,1058,28);status.ForeColor=muted;status.Font=new Font("Segoe UI",9);Controls.Add(status);
        var check=MakeButton(Words.T("Проверить","Check files"),31,825,140,false);check.Click+=(s,e)=>Inspect();
        restore=MakeButton(Words.T("Восстановить (Удалить файлы патчера)","Restore (Remove patcher files)"),184,825,580,false);restore.Click+=(s,e)=>Run(()=>{engine.Restore(folder.Text);Inspect();status.Text=Words.T("Восстановлено состояние до установки 1.5.0.","Restored the state from before installing 1.5.0.");});
        apply=MakeButton(Words.T("Применить","Apply changes"),936,825,153,true);apply.Click+=(s,e)=>Run(()=>{engine.Apply(folder.Text,0);status.Text=Words.T("Готово. Все улучшения установлены. Настраивайте их в меню VR.","Done. All improvements installed. Configure them in VR settings.");});
        var support=MakeButton(Words.T("♥ Поддержать проект","♥ Support the project"),855,31,234,false);support.ForeColor=Color.FromArgb(245,156,182);support.AccessibleName="Support project";support.Click+=(s,e)=>Run(()=>Process.Start(new ProcessStartInfo("https://ko-fi.com/vikaquest"){UseShellExecute=true}));
        BuildRows();Shown+=(s,e)=>Inspect();
    }
    Button MakeButton(string text,int x,int y,int width,bool primary){var b=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,38),FlatStyle=FlatStyle.Flat,BackColor=primary?accent:Theme.Card,ForeColor=primary?Theme.Background:ink};b.FlatAppearance.BorderColor=primary?accent:line;b.FlatAppearance.MouseOverBackColor=primary?Color.FromArgb(132,206,239):Theme.Selected;b.FlatAppearance.MouseDownBackColor=Theme.Selected;Controls.Add(b);return b;}
    void BuildRows(){list.Controls.Clear();rows.Clear();int y=0;foreach(var copy in copies){var f=engine.P.features.First(x=>x.id==copy.id);var row=new FeatureRow(f,copy);row.SetBounds(0,y,346,40);row.TabStop=true;row.Pick+=(s,e)=>SelectFeature(row.Feature.id);row.KeyDown+=(s,e)=>{int i=rows.IndexOf(row);if(e.KeyCode==Keys.Down&&i+1<rows.Count){rows[i+1].Focus();e.Handled=true;}if(e.KeyCode==Keys.Up&&i>0){rows[i-1].Focus();e.Handled=true;}};list.Controls.Add(row);rows.Add(row);y+=43;}count.Text=Words.T("Настройки доступны в игровом меню VR","Configure features in the in-game VR menu");SelectFeature(selectedId??rows[0].Feature.id);}
    public void SelectFeature(string id){var row=rows.FirstOrDefault(x=>x.Feature.id==id);if(row==null)return;selectedId=id;foreach(var r in rows){r.Selected=r==row;r.BackColor=r.Selected?Theme.Selected:Theme.Card;r.Invalidate();}var c=row.Copy;section.Text=Words.T(c.group,c.groupEn).ToUpperInvariant()+Words.T("  /  УПРАВЛЕНИЕ","  /  CONTROLS");heading.Text=Words.T(c.title,c.titleEn);description.Text=Words.T(c.description,c.descriptionEn);conditions.Text=Words.T(c.note,c.noteEn);bindings.Controls.Clear();foreach(var key in Words.English?c.controlsEn:c.controls){bindings.Controls.Add(new Label{Text=key,AutoSize=false,Size=new Size(590,28),Margin=new Padding(0,0,0,4),Padding=new Padding(9,4,4,0),BackColor=Theme.Selected,ForeColor=accent,Font=new Font("Segoe UI",10,FontStyle.Bold)});}if(row.Top<list.VerticalScroll.Value||row.Bottom>list.VerticalScroll.Value+list.ClientSize.Height)list.ScrollControlIntoView(row);}
    public void CaptureGallery(string directory){Directory.CreateDirectory(directory);foreach(var row in rows){SelectFeature(row.Feature.id);Application.DoEvents();using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(0,0,Width,Height));b.Save(Path.Combine(directory,row.Feature.id+".png"));}}}
    public void VerifyUI(){if(rows.Count!=22||rows.Any(x=>x.Controls.OfType<CheckBox>().Any()))throw new Exception("Catalog UI failed");foreach(var row in rows){SelectFeature(row.Feature.id);if(heading.Text.Length==0||description.Text.Length==0||bindings.Controls.Count==0)throw new Exception("Empty feature details");}if(!Controls.OfType<Button>().Any(x=>x.AccessibleName=="Support project"))throw new Exception("Support button missing");var support=Controls.OfType<Button>().First(x=>x.AccessibleName=="Support project");if(Controls.OfType<Label>().Any(x=>x.Bounds.IntersectsWith(support.Bounds)))throw new Exception("Support border is covered by a label");if(restore.Text!=Words.T("Восстановить (Удалить файлы патчера)","Restore (Remove patcher files)"))throw new Exception("Restore caption failed");if(TextRenderer.MeasureText(restore.Text,restore.Font).Width>restore.ClientSize.Width-16)throw new Exception("Restore caption clipped");if(restore.Bottom>ClientSize.Height-15||restore.Bounds.IntersectsWith(status.Bounds))throw new Exception("Footer layout failed");SelectFeature(rows[0].Feature.id);}
    void Inspect(){try{engine.Check(folder.Text);status.Text=Words.T("Найден VR-мод ","Detected VR mod ")+engine.P.version+Words.T(". Перед применением закройте игру. Резервные копии сохраняются в папке игры.",". Close the game before applying. Backups are stored in the game folder.");apply.Enabled=true;}catch(Exception ex){status.Text=Words.Error(ex.Message.Split('\n')[0]);apply.Enabled=false;}}
    void Run(Action action){try{apply.Enabled=restore.Enabled=false;UseWaitCursor=true;action();}catch(Exception ex){status.Text=Words.T("Изменения не применены.","Changes were not applied.");MessageBox.Show(this,Words.Error(ex.Message),Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{UseWaitCursor=false;restore.Enabled=true;apply.Enabled=true;}}
}

public static class Program {
    [STAThread] public static int Main(string[] args) {
        try {
            using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("messages.json"))using(var r=new StreamReader(s,Encoding.UTF8))Words.Errors=Engine.Json.Deserialize<Dictionary<string,string>>(r.ReadToEnd());
            var engine=new Engine();
            if(args.Length>0&&(args[0]=="--render-gallery"||args[0]=="--ui-test")) {
                Application.EnableVisualStyles();using(var f=new MainWindow()){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-30000,-30000);f.Show();Application.DoEvents();f.VerifyUI();if(args[0]=="--render-gallery")f.CaptureGallery(args[1]);f.Close();}return 0;
            }
            if(args.Length>0&&args[0]=="--check-test") {engine.Check(args[1]);return 0;}
            if(args.Length>0&&args[0]=="--apply-test") {engine.Apply(args[1],int.Parse(args[2]));return 0;}
            if(args.Length>0&&args[0]=="--restore-test") {engine.Restore(args[1]);return 0;}
            if(args.Length>0&&args[0]=="--render-ui") {Application.EnableVisualStyles();using(var f=new MainWindow()){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-30000,-30000);f.Show();Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(args[1]);}f.Close();}return 0;}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainWindow());return 0;
        }catch(Exception ex){if(args.Length>0){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-error.txt"),Words.Error(ex.ToString()));return 1;}MessageBox.Show(Words.Error(ex.Message));return 1;}
    }
}
