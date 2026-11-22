using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace HP2ModUpdates
{
    internal sealed class Receipt
    {
        public Release Release {get;set;}
        public Dictionary<string,string> Files {get;set;}
        internal static Receipt Read(Mod mod,string root)
        {
            var file=Catalog.PathIn(root,Transaction.ReceiptName(mod));
            if (!File.Exists(file)) return null;
            try {
                var r=Catalog.Json<Receipt>(File.ReadAllText(file));
                if (r==null || r.Release==null || r.Release.Number==null || !Catalog.IsCommit(r.Release.Commit) || !Catalog.IsHash(r.Release.Hash) ||
                    r.Files==null || !mod.Required.All(r.Files.ContainsKey) || r.Files.Any(x=>!mod.Files.Contains(x.Key) || !Catalog.IsHash(x.Value))) return null;
                return r;
            } catch(ArgumentException) {return null;} catch(InvalidOperationException) {return null;}
        }
        internal bool Matches(string root)
        {
            return Files.All(p=> {var path=Catalog.PathIn(root,p.Key);return File.Exists(path) && Catalog.Digest(path)==p.Value;});
        }
    }

    internal sealed class Updater
    {
        private readonly string root,bundle,executable;
        private readonly IChannel channel;
        private readonly CancellationToken stop;
        private readonly Action<string,string> report;
        private readonly Action idle;
        private readonly Dictionary<string,string> ready=new Dictionary<string,string>();
        internal bool UsedFallback {get; private set;}

        internal Updater(string root,string bundle,string executable,IChannel channel,CancellationToken stop,Action<string,string> report,Action idle)
        { this.root=root;this.bundle=bundle;this.executable=executable;this.channel=channel;this.stop=stop;this.report=report;this.idle=idle; }

        internal void Run()
        {
            idle();
            using(var tx=new Transaction(root)) {
                var lookups=Catalog.Mods.Select(m=>Lookup(()=>channel.Latest(m,stop),m.Key)).ToArray();
                var self=Lookup(()=>channel.Launcher(stop),"launcher");
                var oldCache=new List<string>();
                for(int i=0;i<Catalog.Mods.Length;i++) {
                    stop.ThrowIfCancellationRequested();
                    Plan(Catalog.Mods[i],lookups[i].GetAwaiter().GetResult(),tx,oldCache);
                }
                PlanLauncher(tx,self.GetAwaiter().GetResult());
                stop.ThrowIfCancellationRequested();
                report("status","正在核对并应用更新");
                tx.Commit(delegate {stop.ThrowIfCancellationRequested();idle();});
                foreach(var m in Catalog.Mods) report(m.Key,ready[m.Key]);
                foreach(string path in oldCache) {
                    try {File.Delete(path);} catch(IOException) {} catch(UnauthorizedAccessException) {}
                }
            }
        }
        private Task<T> Lookup<T>(Func<T> fetch,string key) where T:class
        {
            if (channel==null) return Task.FromResult<T>(null);
            return Task.Run(delegate {
                try {return fetch();}
                catch(Exception e) {stop.ThrowIfCancellationRequested();report(key,"版本检查暂不可用 · "+e.Message);return null;}
            },stop);
        }
        private void Plan(Mod mod,Release online,Transaction tx,List<string> oldCache)
        {
            var receipt=Receipt.Read(mod,root);
            var installed=mod.Installed(root);
            bool verified=receipt!=null && receipt.Release.Number==installed && receipt.Matches(root);
            bool healthy=verified || (installed!=null && (receipt==null || receipt.Release.Number!=installed) && mod.Healthy(root));
            var choices=new[]{online,receipt==null?null:receipt.Release,mod.Seed}.Where(r=>r!=null).OrderByDescending(r=>r.Number);
            var tried=new HashSet<string>(StringComparer.Ordinal);
            string error=null;
            foreach(var candidate in choices) {
                stop.ThrowIfCancellationRequested();
                if (!tried.Add(candidate.Version+":"+candidate.Hash) || (installed!=null && candidate.Number<installed)) continue;
                if (verified && receipt.Release.Hash==candidate.Hash) {
                    Done(mod,candidate.Version,online==null || error!=null, "已是最新");return;
                }
                Receipt next;string directory;
                try {
                    string archive=GetPackage(mod,candidate,tx);
                    directory=Path.Combine(tx.Work,"payload-"+Guid.NewGuid().ToString("N"));
                    next=Extract(mod,candidate,archive,directory);
                } catch(Exception e) {
                    stop.ThrowIfCancellationRequested();error=e.Message;report(mod.Key,"安装包暂不可用 · "+error);continue;
                }
                foreach(string name in next.Files.Keys) tx.Put(name,Catalog.PathIn(directory,name));
                tx.PutText(Transaction.ReceiptName(mod),Catalog.Serializer().Serialize(next));
                if (receipt!=null && receipt.Release.Hash!=candidate.Hash && receipt.Release.Hash!=mod.Seed.Hash)
                    oldCache.Add(Cache(receipt.Release.Hash));
                Done(mod,candidate.Version,online==null || error!=null,installed==candidate.Number?"已核验":"准备安装");return;
            }
            if (healthy) {Done(mod,installed.ToString(3),true,"使用本机版本");return;}
            throw new IOException(mod.Title+" 没有可用的完整版本。请保留下载包内的 Packages 文件夹，或连接 GitHub 后重试。"+(error==null?"":"\r\n"+error));
        }
        private void Done(Mod m,string version,bool fallback,string status)
        {
            UsedFallback|=fallback;
            ready[m.Key]=version+" · "+(fallback?"本地可用":"已就绪");
            report(m.Key,version+" · "+(fallback?"本地可用":status));
        }
        private string Cache(string hash)
        {
            if (!Catalog.IsHash(hash)) throw new InvalidDataException("安装包校验值无效");
            return Catalog.PathIn(root,Catalog.State+"/Packages/"+hash.ToLowerInvariant()+".zip");
        }
        private string GetPackage(Mod mod,Release release,Transaction tx)
        {
            string cache=Cache(release.Hash);
            if (File.Exists(cache) && Catalog.Digest(cache)==release.Hash) return cache;
            string local=Path.Combine(bundle,"Packages",mod.Archive(release.Version));
            string temp=Path.Combine(tx.Work,Guid.NewGuid().ToString("N")+".zip");
            if (File.Exists(local) && Catalog.Digest(local)==release.Hash) Transaction.Copy(local,temp);
            else {
                if (channel==null) throw new IOException("本地缺少已校验的 "+mod.Archive(release.Version));
                report(mod.Key,"正在下载 "+release.Version);
                channel.Package(mod,release,temp,stop,p=>report(mod.Key,"正在下载 "+release.Version+" · "+p+"%"));
            }
            if (new FileInfo(temp).Length>128L*1024*1024 || Catalog.Digest(temp)!=release.Hash)
                throw new InvalidDataException("下载校验失败："+mod.Title);
            Transaction.Move(temp,cache);return cache;
        }
        private Receipt Extract(Mod mod,Release release,string archive,string directory)
        {
            var result=new Receipt {Release=release,Files=new Dictionary<string,string>(StringComparer.Ordinal)};
            var names=mod.Files.ToDictionary(p=>p,p=>p,StringComparer.OrdinalIgnoreCase);
            long size=0;Directory.CreateDirectory(directory);
            using(var input=File.OpenRead(archive)) using(var zip=new ZipArchive(input,ZipArchiveMode.Read)) {
                foreach(var entry in zip.Entries) {
                    stop.ThrowIfCancellationRequested();
                    string name;if (!names.TryGetValue(entry.FullName.Replace('\\','/'),out name)) continue;
                    if (result.Files.ContainsKey(name) || entry.Length<0 || entry.Length>128L*1024*1024 || (size+=entry.Length)>192L*1024*1024)
                        throw new InvalidDataException("安装包目录无效");
                    string target=Catalog.PathIn(directory,name);Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using(var data=entry.Open()) using(var dest=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                        byte[] buffer=new byte[81920];long written=0;int n;
                        while ((n=data.Read(buffer,0,buffer.Length))>0) {
                            stop.ThrowIfCancellationRequested();
                            if ((written+=n)>entry.Length) throw new InvalidDataException("文件大小异常："+name);
                            dest.Write(buffer,0,n);
                        }
                        if (written!=entry.Length) throw new EndOfStreamException(name);
                    }
                    result.Files.Add(name,Catalog.Digest(target));
                }
            }
            if (!mod.Required.All(result.Files.ContainsKey) || mod.Installed(directory)!=release.Number || !mod.Healthy(directory))
                throw new InvalidDataException("安装包与游戏版本不匹配："+mod.Title);
            return result;
        }

        internal static ToolRelease CachedLauncher(string root)
        {
            string meta=Catalog.PathIn(root,Catalog.State+"/launcher.json");
            if (!File.Exists(meta)) return null;
            try {
                var r=Catalog.Json<ToolRelease>(File.ReadAllText(meta));
                return ValidTool(root,r)?r:null;
            } catch(ArgumentException) {return null;} catch(InvalidOperationException) {return null;}
        }
        private static bool ValidTool(string root,ToolRelease r)
        {
            if (r==null || Catalog.Number(r.Version)==null || !Catalog.IsHash(r.Hash)) return false;
            string path=Catalog.PathIn(root,Transaction.WorkerName(r.Version));
            return File.Exists(path) && ToolVersion(path)==Catalog.Number(r.Version) && Catalog.Digest(path)==r.Hash;
        }
        internal static Version ToolVersion(string file)
        {
            try {
                var a=AssemblyName.GetAssemblyName(file);
                return a.Name=="HP2-UpdateAndPlay"?Catalog.Number(a.Version.ToString(3)):null;
            } catch(BadImageFormatException) {return null;} catch(IOException) {return null;}
        }
        private void PlanLauncher(Transaction tx,ToolRelease latest)
        {
            Version running=ToolVersion(executable);
            string entry=Catalog.PathIn(root,Catalog.Entry);
            if (running!=null && !Catalog.SamePath(entry,executable)) {
                bool cached=Catalog.SamePath(executable,Catalog.PathIn(root,Transaction.WorkerName(running.ToString(3))));
                Version old=File.Exists(entry)?ToolVersion(entry):null;
                if (!File.Exists(entry) || (!cached && old!=null && old<running)) tx.Put(Catalog.Entry,executable);
            }
            if (latest==null || running==null || Catalog.Number(latest.Version)<=running || ValidTool(root,latest)) return;
            string temp=Path.Combine(tx.Work,"launcher-download.exe");
            try {
                channel.LauncherFile(temp,stop);
                if (!Catalog.IsHash(latest.Hash) || Catalog.Digest(temp)!=latest.Hash || ToolVersion(temp)!=Catalog.Number(latest.Version))
                    throw new InvalidDataException("启动器下载校验失败");
            } catch(Exception e) {stop.ThrowIfCancellationRequested();report("launcher",e.Message);return;}
            tx.Put(Transaction.WorkerName(latest.Version),temp);
            tx.PutText(Catalog.State+"/launcher.json",Catalog.Serializer().Serialize(latest));
        }
    }
}
