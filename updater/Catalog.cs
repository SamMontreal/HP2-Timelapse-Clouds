using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace HP2ModUpdates
{
    internal sealed class Release
    {
        public string Version { get; set; }
        public string Commit { get; set; }
        public string Hash { get; set; }
        public System.Version Number { get { return Catalog.Number(Version); } }
    }

    internal sealed class Mod
    {
        public readonly string Key, Title, Repo;
        public readonly Release Seed;
        public readonly string[] Required, Optional;
        public Mod(string key,string title,string repo,Release seed,string[] required,params string[] optional)
        {
            Key=key; Title=title; Repo=repo; Seed=seed; Required=required; Optional=optional;
        }
        public IEnumerable<string> Files { get { return Required.Concat(Optional); } }
        public string Archive(string version)
        {
            if (Catalog.Number(version)==null) throw new InvalidDataException("版本号无效");
            return Repo+"-v"+version+(Key=="clouds"?"-HP2-0.14":"")+"-Windows-x64.zip";
        }
        public Version Installed(string root)
        {
            string file=Catalog.PathIn(root,Key=="hud"?Required[0]:"Timelapse-Clouds/build-info.json");
            if (!File.Exists(file)) return null;
            try {
                if (Key=="hud") {
                    var a=AssemblyName.GetAssemblyName(file);
                    return a.Name=="HP2-ShiftLight" ? Catalog.Number(a.Version.ToString(3)) : null;
                }
                var info=Catalog.Json<CloudBuild>(File.ReadAllText(file));
                return info!=null && info.port_version=="0.14" ? Catalog.Number(info.version) : null;
            } catch (Exception e) {
                if (!(e is IOException || e is BadImageFormatException || e is ArgumentException || e is InvalidOperationException)) throw;
                return null;
            }
        }
        public bool Healthy(string root)
        {
            if (!FilesExist(root) || Installed(root)==null) return false;
            if (Key=="hud") return true;
            var info=Catalog.Json<CloudBuild>(File.ReadAllText(Catalog.PathIn(root,"Timelapse-Clouds/build-info.json")));
            return Catalog.IsHash(info.cloud_exe_sha256) && Catalog.Digest(Catalog.PathIn(root,"Timelapse-Clouds/speed-clouds.exe"))==info.cloud_exe_sha256.ToLowerInvariant();
        }
        private bool FilesExist(string root) { return Required.All(n=>File.Exists(Catalog.PathIn(root,n))); }
    }

    internal sealed class CloudBuild
    {
        public string version {get;set;}
        public string port_version {get;set;}
        public string cloud_exe_sha256 {get;set;}
    }

    internal static class Catalog
    {
        internal const string State="HP2-ModUpdates", Entry="HP2-UpdateAndPlay.exe";
        internal static readonly Mod[] Mods=BuildMods();
        private static Mod[] BuildMods()
        {
            Dictionary<string,Release> seeds;
            using (var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("HP2.bundled.json"))
            using (var reader=new StreamReader(stream)) seeds=Json<Dictionary<string,Release>>(reader.ReadToEnd());
            return new[]{
                new Mod("hud","换挡仪表","HP2-ShiftLight",seeds["hud"],new[]{"HP2-ShiftLight.exe","Play-HP2-ShiftLight.cmd"},"HP2-ShiftLight-Readme.txt"),
                new Mod("clouds","体积云","HP2-Timelapse-Clouds",seeds["clouds"],new[]{"Timelapse-Clouds/speed-clouds.exe","Timelapse-Clouds/build-info.json",
                    "Play-Volumetric-Clouds.cmd","Timelapse-Clouds/Play-Clouds.cmd"},"Timelapse-Clouds/Play-Clouds-1x.cmd","Timelapse-Clouds/Play-Original-Sky.cmd",
                    "Timelapse-Clouds/使用说明.txt","Timelapse-Clouds/README.md","Timelapse-Clouds/README.zh-CN.md","Timelapse-Clouds/README.en.md")
            };
        }
        internal static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength=2*1024*1024 }; }
        internal static T Json<T>(string text) { return Serializer().Deserialize<T>(text); }
        internal static Version Number(string text)
        {
            Version value;
            return text!=null && Regex.IsMatch(text,@"^\d{1,6}\.\d{1,6}\.\d{1,6}$") && Version.TryParse(text,out value) ? value:null;
        }
        internal static bool IsHash(string text) {return text!=null && Regex.IsMatch(text,"^[a-fA-F0-9]{64}$");}
        internal static bool IsCommit(string text) {return text!=null && Regex.IsMatch(text,"^[a-f0-9]{40}$");}
        internal static string Digest(string file)
        {
            using(var f=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete))
            using(var sha=SHA256.Create()) return Hex(sha.ComputeHash(f));
        }
        internal static string Hex(byte[] hash) {return BitConverter.ToString(hash).Replace("-","").ToLowerInvariant();}
        // Only fixed managed names are passed here. A junction inside an install must
        // never redirect an update into a different game, profile or user folder.
        internal static string PathIn(string root,string relative)
        {
            string start=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string path=Path.GetFullPath(Path.Combine(start,relative.Replace('/',Path.DirectorySeparatorChar)));
            if (!path.StartsWith(start,StringComparison.OrdinalIgnoreCase)) throw new IOException("目标超出游戏目录");
            for (string p=path;p.Length>=start.Length;p=Path.GetDirectoryName(p)) {
                try { if ((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0) throw new IOException("目标是重解析点："+p); }
                catch (FileNotFoundException) {} catch (DirectoryNotFoundException) {}
            }
            return path;
        }
        internal static bool SamePath(string a,string b)
        {
            return String.Equals(Path.GetFullPath(a).TrimEnd('\\'),Path.GetFullPath(b).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase);
        }
    }
}
