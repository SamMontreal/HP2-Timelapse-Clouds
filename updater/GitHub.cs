using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace HP2ModUpdates
{
    internal interface IChannel
    {
        Release Latest(Mod mod,CancellationToken stop);
        void Package(Mod mod,Release release,string file,CancellationToken stop,Action<int> progress);
        ToolRelease Launcher(CancellationToken stop);
        void LauncherFile(string file,CancellationToken stop);
    }

    internal sealed class ToolRelease
    {
        public string Version {get;set;}
        public string Hash {get;set;}
    }
    internal sealed class GitRef
    {
        public string @ref {get;set;}
        public RefObject @object {get;set;}
    }
    internal sealed class RefObject {public string type {get;set;} public string sha {get;set;}}

    internal sealed class GitHub : IChannel
    {
        private const string Raw="https://raw.githubusercontent.com/SamMontreal/";
        private const string Auto=Raw+"HP2-Timelapse-Clouds/auto-update/";
        private static string ReleaseUrl(Mod mod,Release r)
        {
            if (!Catalog.IsCommit(r.Commit)) throw new InvalidDataException("发布记录无效");
            return Raw+mod.Repo+"/"+r.Commit+"/";
        }
        public Release Latest(Mod mod,CancellationToken stop)
        {
            string url="https://api.github.com/repos/SamMontreal/"+mod.Repo+"/git/matching-refs/heads/windows-v";
            var best=mod.Seed;
            const string prefix="refs/heads/windows-v";
            bool more;int page=0;
            do {
                if (++page>100) throw new InvalidDataException("版本列表过长");
                var refs=Catalog.Json<GitRef[]>(Text(url+"?per_page=100&page="+page,stop,out more));
                foreach (var item in refs) {
                    if (item==null || item.@ref==null || !item.@ref.StartsWith(prefix,StringComparison.Ordinal) ||
                        item.@object==null || item.@object.type!="commit" || !Catalog.IsCommit(item.@object.sha)) continue;
                    string number=item.@ref.Substring(prefix.Length);
                    var version=Catalog.Number(number);
                    if (version!=null && version>=best.Number)
                        best=new Release {Version=number,Commit=item.@object.sha};
                }
            }
            while(more);
            if (best.Commit==mod.Seed.Commit) return mod.Seed;
            string archive=mod.Archive(best.Version);
            foreach (var line in Text(ReleaseUrl(mod,best)+"SHA256SUMS.txt",stop).Split('\n')) {
                string[] words=line.Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
                if (words.Length==2 && words[1].TrimStart('*')==archive && Catalog.IsHash(words[0])) {
                    best.Hash=words[0].ToLowerInvariant(); return best;
                }
            }
            throw new InvalidDataException("发布包缺少 SHA-256 校验值");
        }
        public void Package(Mod mod,Release release,string file,CancellationToken stop,Action<int> progress)
        { Download(ReleaseUrl(mod,release)+mod.Archive(release.Version),file,128*1024*1024,stop,progress); }
        public ToolRelease Launcher(CancellationToken stop)
        {
            var r=Catalog.Json<ToolRelease>(Text(Auto+"launcher.json",stop));
            if (r==null || Catalog.Number(r.Version)==null || !Catalog.IsHash(r.Hash)) throw new InvalidDataException("启动器版本信息无效");
            r.Hash=r.Hash.ToLowerInvariant();return r;
        }
        public void LauncherFile(string file,CancellationToken stop) { Download(Auto+Catalog.Entry,file,8*1024*1024,stop,null); }
        private static string Text(string url,CancellationToken stop)
        { bool ignored;return Text(url,stop,out ignored); }
        private static string Text(string url,CancellationToken stop,out bool more)
        {
            using(var stream=new MemoryStream()) {
                string links=Transfer(url,stream,1024*1024,4500,stop,null);
                more=links!=null && links.Contains("rel=\"next\"");
                return Encoding.UTF8.GetString(stream.ToArray()).TrimStart('\uFEFF');
            }
        }
        private static void Download(string url,string file,int limit,CancellationToken stop,Action<int> progress)
        {
            using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                Transfer(url,output,limit,15000,stop,progress);output.Flush(true);
            }
        }
        private static string Transfer(string url,Stream output,int limit,int timeout,CancellationToken stop,Action<int> progress)
        {
            stop.ThrowIfCancellationRequested();
            var req=(HttpWebRequest)WebRequest.Create(url);
            req.UserAgent="HP2-ModUpdater/1.0";req.AllowAutoRedirect=false;req.Timeout=timeout;req.ReadWriteTimeout=timeout;
            using(stop.Register(req.Abort))
            using(var response=(HttpWebResponse)req.GetResponse())
            using(var input=response.GetResponseStream()) {
                long size=response.ContentLength;
                if (response.StatusCode!=HttpStatusCode.OK || size>limit) throw new IOException("下载响应无效");
                byte[] block=new byte[81920];int n,old=-1;long total=0;
                while ((n=input.Read(block,0,block.Length))>0) {
                    stop.ThrowIfCancellationRequested(); total+=n;
                    if (total>limit) throw new IOException("下载文件过大");
                    output.Write(block,0,n);
                    if (progress!=null && size>0) {int p=(int)(100*total/size);if (p!=old) {progress(p);old=p;}}
                }
                if (size>=0 && total!=size) throw new EndOfStreamException("下载不完整");
                return response.Headers["Link"];
            }
        }
    }
}
