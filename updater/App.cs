using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("HP2 Mod Updater")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace HP2ModUpdates
{
    internal sealed class InUse : IOException {internal InUse():base("游戏或仪表正在运行。下次从此入口启动时会更新。") {}}
    internal sealed class Settings
    {
        internal string Root,Bundle;internal bool Quiet,UpdateOnly,Offline;
        internal Settings(string[] args)
        {
            for(int i=0;i<args.Length;i++) {
                switch(args[i]) {
                    case "--game-dir": if (++i>=args.Length) throw new ArgumentException("缺少游戏目录");Root=Path.GetFullPath(args[i]);break;
                    case "--bundle-dir": if (++i>=args.Length) throw new ArgumentException("缺少下载包目录");Bundle=Path.GetFullPath(args[i]);break;
                    case "--update-only":UpdateOnly=true;break;
                    case "--offline":Offline=true;break;
                    case "--quiet":Quiet=true;break;
                    default:throw new ArgumentException("无法识别的选项："+args[i]);
                }
            }
            if (Bundle==null) Bundle=AppDomain.CurrentDomain.BaseDirectory;
        }
        private static string Quote(string path)
        {
            if (path.Contains("\"")) throw new ArgumentException("目录无效");
            return "\""+path+new string('\\',path.Length-path.TrimEnd('\\').Length)+"\"";
        }
        internal string Forward() {return "--game-dir "+Quote(Root)+" --bundle-dir "+Quote(Bundle)+(Offline?" --offline":"")+(Quiet?" --quiet":"")+(UpdateOnly?" --update-only":"");}
    }

    internal static class App
    {
        internal static string Executable {get {return Assembly.GetExecutingAssembly().Location;}}
        private static bool LooksLikeGame(string root)
        {return !String.IsNullOrEmpty(root) && File.Exists(Path.Combine(root,"speed.exe")) && File.Exists(Path.Combine(root,"data","TRACKS","TEX32TRACK.tex"));}
        private static string RememberFile {get {return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HP2-ModUpdater","game-folder.txt");}}
        private static string FindGame(Settings options,out bool picked)
        {
            picked=false;
            if (options.Root!=null) {
                if (!LooksLikeGame(options.Root)) throw new IOException("请选择包含 speed.exe 和 data 文件夹的 HP2 0.14 游戏目录。");
                return options.Root;
            }
            var dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for(int i=0;dir!=null && i<3;i++,dir=dir.Parent) if (LooksLikeGame(dir.FullName)) return dir.FullName;
            if (!options.Quiet) {
                try {if (File.Exists(RememberFile)) {string p=File.ReadAllText(RememberFile);if (LooksLikeGame(p)) return p;}} catch(IOException) {} catch(UnauthorizedAccessException) {}
                using(var dialog=new FolderBrowserDialog {Description="选择 HP2 0.14 游戏目录（内含 speed.exe 和 data 文件夹）",ShowNewFolderButton=false}) {
                    while(dialog.ShowDialog()==DialogResult.OK) {
                        if (LooksLikeGame(dialog.SelectedPath)) {picked=true;return dialog.SelectedPath;}
                        MessageBox.Show("这个目录里找不到 HP2 0.14 游戏文件。","选择游戏目录",MessageBoxButtons.OK,MessageBoxIcon.Information);
                    }
                    return null;
                }
            }
            throw new IOException("未找到游戏目录，请使用 --game-dir 指定。");
        }
        internal static void CheckIdle(string root)
        {
            foreach(string rel in new[]{"speed.exe","Timelapse-Clouds/speed-clouds.exe","HP2-ShiftLight.exe"}) {
                string path=Path.GetFullPath(Path.Combine(root,rel));
                foreach(var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(rel))) using(p) {
                    string location;
                    try {location=p.MainModule.FileName;} catch(Win32Exception) {continue;} catch(InvalidOperationException) {continue;}
                    if (Catalog.SamePath(location,path)) throw new InUse();
                }
            }
        }
        private static void StartGame(string root)
        {
            CheckIdle(root);
            var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cmd.exe"),"/d /c .\\Play-HP2-ShiftLight.cmd") {
                WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true };
            using(var p=Process.Start(info)) {
                if (p.WaitForExit(1200) && p.ExitCode!=0) throw new IOException("游戏启动器返回错误 "+p.ExitCode);
            }
        }
        private static bool CanUseExisting(string root)
        {
            try {
                if (File.Exists(Catalog.PathIn(root,Catalog.State+"/transaction/pending.json"))) return false;
                foreach(var mod in Catalog.Mods) {
                    var receipt=Receipt.Read(mod,root);
                    if (receipt!=null && receipt.Release.Number==mod.Installed(root)) {
                        if (!receipt.Matches(root)) return false;
                    } else if (!mod.Healthy(root)) return false;
                }
                return true;
            } catch(IOException) {return false;} catch(UnauthorizedAccessException) {return false;}
        }
        internal static void Execute(Settings options,CancellationToken stop,Action<string,string> ui)
        {
            CheckIdle(options.Root);
            var lines=new List<string>{"HP2 Mod Updater "+Assembly.GetExecutingAssembly().GetName().Version.ToString(3)};
            Action<string,string> report=(key,text)=> {
                if (!text.EndsWith("%",StringComparison.Ordinal)) lock(lines) lines.Add(DateTime.Now.ToString("HH:mm:ss")+" "+key+" "+text);
                ui(key,text);
            };
            try {
                var updater=new Updater(options.Root,options.Bundle,Executable,options.Offline?null:new GitHub(),stop,report,()=>CheckIdle(options.Root));
                try {
                    updater.Run();report("status",updater.UsedFallback?"本机版本已就绪":"更新完成");
                } catch(Exception e) {
                    stop.ThrowIfCancellationRequested();
                    if (e is InUse || (!(e is IOException) && !(e is UnauthorizedAccessException)) || options.UpdateOnly || !CanUseExisting(options.Root)) throw;
                    report("status","更新暂缓，使用已安装版本 · "+e.Message);
                }
                stop.ThrowIfCancellationRequested();
                if (!options.UpdateOnly) StartGame(options.Root);
            } catch(Exception e) {
                report("status",e.Message);throw;
            } finally {
                try {
                    string file=Catalog.PathIn(options.Root,Catalog.State+"/last-run.log");
                    if (Directory.Exists(Path.GetDirectoryName(file))) lock(lines) File.WriteAllLines(file,lines,new UTF8Encoding(true));
                } catch(IOException) {} catch(UnauthorizedAccessException) {}
            }
        }
        [STAThread] public static int Main(string[] args)
        {
            bool quiet=Array.IndexOf(args,"--quiet")>=0;
            try {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
                var options=new Settings(args);bool picked;
                options.Root=FindGame(options,out picked);
                if (options.Root==null) return 3;
                CheckIdle(options.Root);
                if (!File.Exists(Catalog.PathIn(options.Root,Catalog.State+"/transaction/pending.json"))) {
                    var cached=Updater.CachedLauncher(options.Root);
                    if (cached!=null && Catalog.Number(cached.Version)>Updater.ToolVersion(Executable)) {
                        using(var child=Process.Start(new ProcessStartInfo(Catalog.PathIn(options.Root,Transaction.WorkerName(cached.Version)),options.Forward()){UseShellExecute=false})) {
                            if (quiet) {child.WaitForExit();return child.ExitCode;}return 0;
                        }
                    }
                }
                string key;
                using(var hash=SHA256.Create()) key=Catalog.Hex(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(options.Root).TrimEnd('\\').ToUpperInvariant())));
                using(var mutex=new Mutex(false,@"Local\HP2ModsUpdate-"+key)) {
                    bool owned;try {owned=mutex.WaitOne(0);} catch(AbandonedMutexException) {owned=true;}
                    if (!owned) {if (!quiet) MessageBox.Show("更新器已经在运行。","HP2 模组更新");return 2;}
                    try {
                        int result;
                        if (quiet) {Execute(options,CancellationToken.None,delegate{});result=0;}
                        else using(var form=new UpdateWindow(options)) {Application.Run(form);result=form.Result;}
                        if (result==0 && picked) try {Directory.CreateDirectory(Path.GetDirectoryName(RememberFile));File.WriteAllText(RememberFile,options.Root);} catch(IOException) {} catch(UnauthorizedAccessException) {}
                        return result;
                    } finally {mutex.ReleaseMutex();}
                }
            } catch(Exception e) {
                if (!quiet) MessageBox.Show(e.Message,"HP2 模组更新",MessageBoxButtons.OK,e is InUse?MessageBoxIcon.Information:MessageBoxIcon.Warning);
                else Console.Error.WriteLine(e.Message);
                return e is InUse ? 2 : 1;
            }
        }
    }

    internal sealed class UpdateWindow : Form
    {
        private readonly Settings options;
        private readonly Label hud=new Label(),clouds=new Label(),status=new Label();
        private readonly Button button=new Button();
        private readonly ProgressBar progress=new ProgressBar();
        private readonly CancellationTokenSource cancel=new CancellationTokenSource();
        private bool done;internal int Result=1;
        internal UpdateWindow(Settings options)
        {
            this.options=options;
            Text="HP2 模组更新";StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;
            MaximizeBox=false;ClientSize=new Size(510,226);AutoScaleMode=AutoScaleMode.Dpi;
            Font=new Font("Microsoft YaHei UI",9);BackColor=Color.FromArgb(30,30,32);ForeColor=Color.White;
            AddLabel(hud,"换挡仪表    正在检查",20,22,470,27);AddLabel(clouds,"体积云        正在检查",20,56,470,27);
            progress.SetBounds(22,100,465,8);progress.Style=ProgressBarStyle.Marquee;Controls.Add(progress);
            AddLabel(status,options.Offline?"正在检查本地文件":"正在检查 GitHub 版本",20,124,470,40);
            button.Text="取消";button.SetBounds(395,176,92,33);button.FlatStyle=FlatStyle.Flat;Controls.Add(button);
            button.Click+=delegate {Close();};Shown+=Start;
            FormClosing+=delegate(object sender,FormClosingEventArgs e) {if (!done) {e.Cancel=true;cancel.Cancel();button.Enabled=false;status.Text="正在结束更新";}};
        }
        private void AddLabel(Label label,string text,int x,int y,int w,int h)
        {label.Text=text;label.SetBounds(x,y,w,h);label.AutoEllipsis=true;Controls.Add(label);}
        private async void Start(object sender,EventArgs args)
        {
            IProgress<KeyValuePair<string,string>> output=new Progress<KeyValuePair<string,string>>(p=>{
                if (done || IsDisposed) return;
                if (p.Key=="hud") hud.Text="换挡仪表    "+p.Value;
                else if (p.Key=="clouds") clouds.Text="体积云        "+p.Value;
                else if (p.Key=="status") status.Text=p.Value;
            });
            try {
                await Task.Run(()=>App.Execute(options,cancel.Token,(key,msg)=>output.Report(new KeyValuePair<string,string>(key,msg))));
                Result=0;done=true;
                if (!options.UpdateOnly) {Close();return;}
                status.Text="两个模组均已就绪";
            } catch(OperationCanceledException) {Result=3;done=true;Close();return;}
              catch(Exception e) {
                Result=e is InUse?2:1;done=true;
                status.Text=e is InUse?e.Message:"更新未完成";if (!(e is InUse)) MessageBox.Show(this,e.Message,"HP2 模组更新",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
            progress.Style=ProgressBarStyle.Blocks;progress.Value=Result==0?100:0;button.Text="关闭";button.Enabled=true;
        }
        protected override void Dispose(bool value) {if (value) cancel.Dispose();base.Dispose(value);}
    }
}
