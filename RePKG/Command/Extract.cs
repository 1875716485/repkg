﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json;
using RePKG.Application.Texture;
using RePKG.Core.Texture;
using RePKG.Package;

namespace RePKG.Command
{
    public static class Extract
    {
        private static ExtractOptions _options;
        private static string[] _skipExtArray;
        private static string[] _onlyExtArray;
        private static readonly string[] ProjectFiles = {"project.json"};

        private static readonly ITexReader _texReader;
        private static readonly ITexJsonInfoGenerator _texJsonInfoGenerator;

        static Extract()
        {
            var texHeaderReader = new TexHeaderReader();
            var texMipmapDecompressor = new TexMipmapDecompressor();
            var texMipmapReader = new TexMipmapReader(texMipmapDecompressor);
            var texMipmapContainerReader = new TexMipmapContainerReader(texMipmapReader);

            _texReader = new TexReader(texHeaderReader, texMipmapContainerReader);
            _texJsonInfoGenerator = new TexJsonInfoGenerator();
        }

        public static void Action(ExtractOptions options)
        {
            _options = options;

            if (string.IsNullOrEmpty(options.OutputDirectory))
            {
                options.OutputDirectory = Directory.GetCurrentDirectory();
            }

            if (!string.IsNullOrEmpty(_options.IgnoreExts))
                _skipExtArray = NormalizeExtensions(_options.IgnoreExts.Split(','));

            if (!string.IsNullOrEmpty(_options.OnlyExts))
                _onlyExtArray = NormalizeExtensions(_options.OnlyExts.Split(','));

            var fileInfo = new FileInfo(options.Input);
            var directoryInfo = new DirectoryInfo(options.Input);

            if (!fileInfo.Exists)
            {
                if (directoryInfo.Exists)
                {
                    if (_options.TexDirectory)
                        ExtractTexDirectory(directoryInfo);
                    else
                        ExtractPkgDirectory(directoryInfo);

                    Console.WriteLine("Done");
                    return;
                }

                Console.WriteLine("Input file not found");
                Console.WriteLine(options.Input);
                return;
            }

            ExtractFile(fileInfo);
            Console.WriteLine("Done");
        }

        private static string[] NormalizeExtensions(string[] array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].StartsWith("."))
                    continue;
                array[i] = '.' + array[i];
            }

            return array;
        }

        private static void ExtractTexDirectory(DirectoryInfo directoryInfo)
        {
            var flags = SearchOption.TopDirectoryOnly;

            if (_options.Recursive)
                flags = SearchOption.AllDirectories;

            Directory.CreateDirectory(_options.OutputDirectory);

            foreach (var fileInfo in directoryInfo.EnumerateFiles("*.tex", flags))
            {
                if (!fileInfo.Extension.Equals(".tex", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var tex = LoadTex(File.ReadAllBytes(fileInfo.FullName), fileInfo.FullName);

                    if (tex == null)
                        continue;

                    var filePath = Path.Combine(_options.OutputDirectory,
                        Path.GetFileNameWithoutExtension(fileInfo.Name));

                    TexPreviewWriter.WriteTexture(tex, filePath, _options.Overwrite);
                    var jsonInfo = _texJsonInfoGenerator.GenerateInfo(tex);
                    File.WriteAllText($"{filePath}.tex-json", jsonInfo);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to write texture");
                    Console.WriteLine(e);
                }
            }
        }

        private static void ExtractPkgDirectory(DirectoryInfo directoryInfo)
        {
            var rootDirectoryLength = directoryInfo.FullName.Length + 1;

            if (_options.Recursive)
            {
                foreach (var file in directoryInfo.EnumerateFiles("*.pkg", SearchOption.AllDirectories))
                {
                    if (file.Directory == null || file.Directory.FullName.Length < rootDirectoryLength)
                        ExtractPkg(file);
                    else
                        ExtractPkg(file, true, file.Directory.FullName.Substring(rootDirectoryLength));
                }

                return;
            }

            foreach (var directory in directoryInfo.EnumerateDirectories())
            {
                foreach (var file in directory.EnumerateFiles("*.pkg"))
                {
                    ExtractPkg(file, true, directory.FullName.Substring(rootDirectoryLength));
                }
            }
        }

        private static void ExtractFile(FileInfo fileInfo)
        {
            Directory.CreateDirectory(_options.OutputDirectory);

            if (fileInfo.Extension.Equals(".pkg", StringComparison.OrdinalIgnoreCase))
                ExtractPkg(fileInfo);
            else if (fileInfo.Extension.Equals(".tex", StringComparison.OrdinalIgnoreCase))
            {
                var tex = LoadTex(File.ReadAllBytes(fileInfo.FullName), fileInfo.FullName);

                if (tex == null)
                    return;

                try
                {
                    var filePath = Path.Combine(_options.OutputDirectory,
                        Path.GetFileNameWithoutExtension(fileInfo.Name));

                    TexPreviewWriter.WriteTexture(tex, filePath, _options.Overwrite);
                    var jsonInfo = _texJsonInfoGenerator.GenerateInfo(tex);
                    File.WriteAllText($"{filePath}.tex-json", jsonInfo);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"");
                    Console.WriteLine(e);
                }
            }
            else
                Console.WriteLine($"Unrecognized file extension: {fileInfo.Extension}");
        }

        private static void ExtractPkg(FileInfo file, bool appendFolderName = false, string defaultProjectName = "")
        {
            Console.WriteLine($"\r\n### Extracting package: {file.FullName}");

            // Load package
            var loader = new PackageLoader(true);
            var package = loader.Load(file);

            // Get output directory
            string outputDirectory;
            var preview = string.Empty;
            if (appendFolderName)
                GetProjectFolderNameAndPreviewImage(file, defaultProjectName, out outputDirectory, out preview);
            else
                outputDirectory = _options.OutputDirectory;

            // Extract package entries
            var entries = FilterEntries(package.Entries);
            foreach (var entry in entries)
            {
                ExtractEntry(entry, ref outputDirectory);
            }

            // Copy project files project.json/preview image
            if (!_options.CopyProject || _options.SingleDir || file.Directory == null)
                return;

            var files = file.Directory.GetFiles().Where(x =>
                x.Name.Equals(preview, StringComparison.OrdinalIgnoreCase) ||
                ProjectFiles.Contains(x.Name, StringComparer.OrdinalIgnoreCase));

            CopyFiles(files, outputDirectory);
        }

        private static void CopyFiles(IEnumerable<FileInfo> files, string outputDirectory)
        {
            foreach (var file in files)
            {
                var outputPath = Path.Combine(outputDirectory, file.Name);

                if (!_options.Overwrite && File.Exists(outputPath))
                    Console.WriteLine($"* Skipping, already exists: {outputPath}");
                else
                {
                    File.Copy(file.FullName, outputPath, true);
                    Console.WriteLine($"* Copying: {file.FullName}");
                }
            }
        }

        private static IEnumerable<Entry> FilterEntries(IEnumerable<Entry> entries)
        {
            if (!string.IsNullOrEmpty(_options.IgnoreExts))
            {
                return from entry in entries
                    where !_skipExtArray.Any(s => entry.FullName.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                    select entry;
            }

            if (!string.IsNullOrEmpty(_options.OnlyExts))
            {
                return from entry in entries
                    where _onlyExtArray.Any(s => entry.FullName.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                    select entry;
            }

            return entries;
        }

        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        private static void ExtractEntry(Entry entry, ref string outputDirectory)
        {
            if (Program.Closing)
                Environment.Exit(0);

            // save raw
            var filePath = _options.SingleDir
                ? Path.Combine(outputDirectory, entry.Name)
                : Path.Combine(outputDirectory, entry.EntryPath, entry.Name);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            var outputPath = filePath + entry.Extension;

            if (!_options.Overwrite && File.Exists(outputPath))
                Console.WriteLine($"* Skipping, already exists: {outputPath}");
            else
            {
                Console.WriteLine($"* Extracting: {entry.FullName}");
                entry.WriteTo(outputPath);
            }

            // decompile and save
            if (_options.NoTexDecompile || entry.Type != EntryType.Tex)
                return;


            var tex = LoadTex(entry.Data, entry.FullName);

            if (tex == null)
                return;

            try
            {
                TexPreviewWriter.WriteTexture(tex, filePath, _options.Overwrite);
                var jsonInfo = _texJsonInfoGenerator.GenerateInfo(tex);
                File.WriteAllText($"{filePath}.tex-json", jsonInfo);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to write texture");
                Console.WriteLine(e);
            }
        }

        private static void GetProjectInfo(FileInfo packageFile, ref string title, ref string preview)
        {
            var directory = packageFile.Directory;
            if (directory == null)
                return;

            var projectJson = directory.GetFiles("project.json");
            if (projectJson.Length == 0 || !projectJson[0].Exists)
                return;

            dynamic json = JsonConvert.DeserializeObject(File.ReadAllText(projectJson[0].FullName));
            title = json.title;
            preview = json.preview;
        }

        private static void GetProjectFolderNameAndPreviewImage(FileInfo packageFile, string defaultProjectName,
            out string outputDirectory, out string preview)
        {
            preview = string.Empty;

            if (_options.SingleDir)
            {
                outputDirectory = _options.OutputDirectory;
                return;
            }

            if (_options.UseName)
            {
                var name = defaultProjectName;
                GetProjectInfo(packageFile, ref name, ref preview);
                outputDirectory = Path.Combine(_options.OutputDirectory, name.GetSafeFilename());
                return;
            }

            outputDirectory = Path.Combine(_options.OutputDirectory, defaultProjectName);
        }

        private static Tex LoadTex(byte[] bytes, string name)
        {
            if (Program.Closing)
                Environment.Exit(0);

            Console.WriteLine("* Reading: {0}", name);

            try
            {
                return _texReader.ReadFromStream(new MemoryStream(bytes));
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to read texture");
                Console.WriteLine(e);
            }

            return null;
        }
    }


    [Verb("extract", HelpText = "Extract .pkg/Decompile .tex")]
    public class ExtractOptions
    {
        [Option('o', "output", Required = false, HelpText = "Output directory", Default = "./output")]
        public string OutputDirectory { get; set; }

        [Option('i', "ignoreexts", HelpText =
            "Don't extract files with specified extensions (delimited by comma \",\")")]
        public string IgnoreExts { get; set; }

        [Option('e', "onlyexts", HelpText = "Only extract files with specified extensions (delimited by comma \",\")")]
        public string OnlyExts { get; set; }

        [Option('t', "tex", HelpText = "Decompile all tex files from specified directory in input")]
        public bool TexDirectory { get; set; }

        [Option('s', "singledir", HelpText =
            "Should all extracted files be put in one directory instead of their entry path")]
        public bool SingleDir { get; set; }

        [Option('r', "recursive", HelpText = "Recursive search in all subfolders of specified directory")]
        public bool Recursive { get; set; }

        [Option('c', "copyproject", HelpText =
            "Copy project.json and preview.jpg from beside .pkg into output directory")]
        public bool CopyProject { get; set; }

        [Option('n', "usename", HelpText = "Use name from project.json as project subfolder name instead of id")]
        public bool UseName { get; set; }

        [Option("no-tex-decompile", HelpText = "Don't decompile .tex files while extracting .pkg")]
        public bool NoTexDecompile { get; set; }

        [Option("overwrite", HelpText = "Overwrite all existing files")]
        public bool Overwrite { get; set; }

        [Value(0, Required = true, HelpText = "Path to file/directory", MetaName = "Input")]
        public string Input { get; set; }
    }
}