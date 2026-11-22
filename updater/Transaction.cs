using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HP2ModUpdates
{
    internal sealed class Change
    {
        public string Name {get;set;}
        public string Before {get;set;}
        public string After {get;set;}
    }

    internal sealed class Transaction : IDisposable
    {
        private readonly string root, journal;
        internal readonly string Work;
        private readonly List<Change> items=new List<Change>();
        private bool closed;

        internal Transaction(string root)
        {
            this.root=root;Work=Catalog.PathIn(root,Catalog.State+"/transaction");
            journal=Path.Combine(Work,"pending.json");
            Recover(root);
            Directory.CreateDirectory(Work);
        }
        internal static void Move(string from,string to)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            if (!MoveFileEx(from,to,9)) throw new IOException("无法替换 "+Path.GetFileName(to),new Win32Exception(Marshal.GetLastWin32Error()));
        }
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
        private static extern bool MoveFileEx(string from,string to,int flags);
        internal static void Copy(string from,string to)
        {
            using(var src=File.OpenRead(from)) using(var dst=new FileStream(to,FileMode.Create,FileAccess.Write,FileShare.None)) {
                src.CopyTo(dst);dst.Flush(true);
            }
        }
        private static void WriteText(string path,string text)
        {
            byte[] bytes=new UTF8Encoding(false).GetBytes(text);
            using(var file=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None)) {file.Write(bytes,0,bytes.Length);file.Flush(true);}
        }
        internal void PutText(string name,string text)
        {
            string temp=Path.Combine(Work,"text.tmp");WriteText(temp,text);Put(name,temp);
        }
        internal void Put(string name,string source)
        {
            if (!Allowed(name) || items.Any(i=>i.Name==name)) throw new InvalidDataException("安装路径无效："+name);
            string dest=Catalog.PathIn(root,name),after=Catalog.Digest(source),before=File.Exists(dest)?Catalog.Digest(dest):null;
            if (after==before) return;
            Copy(source,Path.Combine(Work,"new-"+items.Count));
            items.Add(new Change {Name=name,Before=before,After=after});
        }
        internal static string ReceiptName(Mod m) {return Catalog.State+"/"+m.Key+".json";}
        internal static string WorkerName(string v)
        {
            if (Catalog.Number(v)==null) throw new InvalidDataException("启动器版本无效");
            return Catalog.State+"/launcher/v"+v+"/"+Catalog.Entry;
        }
        private static bool Allowed(string p)
        {
            return p==Catalog.Entry || p==Catalog.State+"/launcher.json" ||
                Catalog.Mods.Any(m=>m.Files.Contains(p,StringComparer.Ordinal) || ReceiptName(m)==p) ||
                (p!=null && Regex.IsMatch(p,@"^HP2-ModUpdates/launcher/v[0-9]{1,6}\.[0-9]{1,6}\.[0-9]{1,6}/HP2-UpdateAndPlay\.exe$"));
        }
        internal void Commit(Action beforeWrite)
        {
            beforeWrite();
            for(int i=0;i<items.Count;i++) {
                var c=items[i];string dest=Catalog.PathIn(root,c.Name);
                if (c.Before==null) {if (File.Exists(dest)) throw new IOException("安装期间文件被更改："+c.Name);}
                else {
                    string backup=Path.Combine(Work,"old-"+i);Copy(dest,backup);
                    if (Catalog.Digest(backup)!=c.Before) throw new IOException("安装期间文件被更改："+c.Name);
                }
            }
            WriteText(journal+".tmp",Catalog.Serializer().Serialize(items));
            Move(journal+".tmp",journal);
            try {
                for(int i=0;i<items.Count;i++) {
                    beforeWrite();
                    Move(Path.Combine(Work,"new-"+i),Catalog.PathIn(root,items[i].Name));
                }
                Move(journal,Path.Combine(Work,"complete"));
            } catch {
                Recover(root);closed=true;throw;
            }
            closed=true;Clean(root,Work);
        }
        internal static void Recover(string root)
        {
            string dir=Catalog.PathIn(root,Catalog.State+"/transaction");
            if (!Directory.Exists(dir)) return;
            string journal=Catalog.PathIn(root,Catalog.State+"/transaction/pending.json");
            if (File.Exists(journal)) {
                var list=Catalog.Json<List<Change>>(File.ReadAllText(journal));
                if (list==null || list.Count>64 || list.Any(c=>c==null || !Allowed(c.Name) || !Catalog.IsHash(c.After) ||
                    (c.Before!=null && !Catalog.IsHash(c.Before))) || list.Select(c=>c.Name).Distinct().Count()!=list.Count)
                    throw new InvalidDataException("恢复记录无效，备份位于 "+dir);
                for(int i=list.Count-1;i>=0;i--) {
                    var c=list[i];string dest=Catalog.PathIn(root,c.Name);
                    string actual=File.Exists(dest)?Catalog.Digest(dest):null;
                    if (actual==c.Before) continue;
                    if (actual!=null && actual!=c.After) throw new IOException("恢复时发现文件已被更改，备份位于 "+dir);
                    if (c.Before==null) File.Delete(dest);
                    else {
                        string backup=Catalog.PathIn(root,Catalog.State+"/transaction/old-"+i);
                        if (!File.Exists(backup) || Catalog.Digest(backup)!=c.Before) throw new IOException("原文件备份不完整："+c.Name);
                        string temp=Catalog.PathIn(root,Catalog.State+"/transaction/restore.tmp");
                        Copy(backup,temp);Move(temp,dest);
                    }
                }
                Move(journal,Path.Combine(dir,"restored"));
            }
            Clean(root,dir);
        }
        private static void Clean(string root,string dir)
        {
            string expected=Catalog.PathIn(root,Catalog.State+"/transaction");
            string full=Path.GetFullPath(dir);
            if (!Catalog.SamePath(full,expected) && !full.StartsWith(expected+"\\",StringComparison.OrdinalIgnoreCase))
                throw new IOException("临时目录不正确");
            if (!Directory.Exists(full)) return;
            foreach(var path in Directory.GetFileSystemEntries(full)) {
                var flags=File.GetAttributes(path);
                if ((flags&FileAttributes.ReparsePoint)!=0) throw new IOException("临时目录包含重解析点："+path);
                if ((flags&FileAttributes.Directory)!=0) Clean(root,path);else File.Delete(path);
            }
            Directory.Delete(full,false);
        }
        public void Dispose()
        {
            if (!closed && !File.Exists(journal)) Clean(root,Work);
        }
    }
}