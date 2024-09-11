﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LuaDecryptor
{
    public class Lua
    {        
        public string luabytes {  get; set; }
        public int dumpCount {  get; set; }
        public string luaCompiled { get; set; }
        public string luaRestored { get; set; }
        
        public Lua(string luabytes) 
        {
            this.luabytes = luabytes;
            luaCompiled = "Compiled_Lua";
            luaRestored = "LuaData";
            dumpCount = 0;
        }
        public void DecryptLuabytes()
        {
            var files = Directory.GetFiles(luabytes, "*.dat", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var filename = Path.GetFileNameWithoutExtension(file);
                Console.WriteLine($"Decrypting {filename}");
                var reader = new BinaryReader(File.Open(file, FileMode.Open));
                reader.ReadBytes(48);
                while (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    var key = reader.ReadBytes(reader.ReadChar());
                    var str = Encoding.UTF8.GetString(key).ToLower();
                    var data = reader.ReadBytes(reader.ReadInt32());
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] ^= key[i % key.Length];
                    }

                    using (var stream = new MemoryStream(data))
                    using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
                    using (var decompressed = new MemoryStream())
                    {
                        gzip.CopyTo(decompressed);
                        data = decompressed.ToArray();
                    }

                    Directory.CreateDirectory(Path.Combine(luaCompiled, filename));
                    File.WriteAllBytes(Path.Combine(luaCompiled, filename, str), data);
                    dumpCount++;
                }
            }
            Console.WriteLine($"Decrypt {dumpCount} lua files.");
        }

        public void DecompileLua()
        {
            if (dumpCount == 0)
            {
                Console.WriteLine("No decrypted lua found.");
            }
            else
            {
                if (!Directory.Exists(luaRestored))
                    Directory.CreateDirectory(luaRestored);
                ProcessStartInfo info = new ProcessStartInfo("luajit-decompiler-v2.exe");
                info.Arguments = $"{luaCompiled} -s -o {luaRestored}";
                info.UseShellExecute = false;
                var process = new Process();
                process.StartInfo = info;
                process.Start();
                process.WaitForExit();
            }            
        }

        public void restoreLuaName()
        {
            var files = Directory.GetFiles(luaRestored, "*.lua*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                Console.WriteLine(file);
                string moduleCheck = File.ReadLines(file).FirstOrDefault();
                if (moduleCheck != null)
                {
                    if (moduleCheck.StartsWith("module(\""))
                    {
                        Console.WriteLine("Find modulde header, restore original path and name...");

                        //check for module header at start of file, if not have try restore using hardcoded func name
                        int firstIndex = moduleCheck.IndexOf('"') + 1;
                        int secondIndex = moduleCheck.IndexOf('"', moduleCheck.IndexOf('"') + 1) - 1;
                        string subDirFullPath = moduleCheck.Substring(firstIndex, secondIndex - firstIndex + 1);
                        Console.WriteLine(subDirFullPath);
                        string[] subDir = subDirFullPath.Split('.');
                        string fileName = $"{subDir[subDir.Length - 1]}.lua";
                        subDir = subDir.Take(subDir.Count() - 1).ToArray();
                        string subDirCombine = Path.Combine(subDir);

                        try
                        {
                            Directory.CreateDirectory(Path.Combine(luaRestored, subDirCombine));
                            Console.WriteLine($"Move {Path.GetFileName(file)} -> {Path.Combine(luaRestored, subDirCombine, fileName)}");
                            File.Move(file, Path.Combine(luaRestored, subDirCombine, fileName), true);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }

                    }
                    else
                    {
                        //try recovering using luaname.txt
                        //idea from @yarik
                        var names = new Dictionary<string, string>();
                        foreach (var name in File.ReadAllLines(@"luaName.txt"))
                        {
                            names.Add(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(name))).ToLower(), name);
                        }
                        if (names.TryGetValue(Path.GetFileName(file), out var path))
                        {
                            try
                            {
                                Console.WriteLine($"Move {Path.GetFileName(file)} -> {Path.Combine(luaRestored, Path.GetDirectoryName(path))}");
                                Directory.CreateDirectory(Path.Combine(luaRestored, Path.GetDirectoryName(path)));
                                File.Move(file, Path.Combine(luaRestored, path), true);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        }

                    }

                }
            }
        }

        public void checkHardCodeLuaName()
        {
            string luaDump = @"DumpHC";
            var files = Directory.GetFiles(luaDump, "*.lua*", SearchOption.AllDirectories);
            using (StreamWriter sw = new StreamWriter("luaName.txt"))
            {
                foreach (var f in files)
                {
                    string moduleCheck = File.ReadLines(f).FirstOrDefault();
                    if (moduleCheck != null && !moduleCheck.StartsWith("module(\""))
                    {
                        sw.WriteLine(f.Replace($"{luaDump}\\", string.Empty));
                    }
                }
            }
        }

        public void checkHash()
        {
            

        }
    }
}
