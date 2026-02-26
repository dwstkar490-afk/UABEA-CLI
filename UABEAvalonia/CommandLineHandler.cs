using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UABEAvalonia
{
    public class CommandLineHandler
    {
        public static void PrintHelp()
        {
            Console.WriteLine("UABE AVALONIA");
            Console.WriteLine("WARNING: Command line support VERY EARLY");
            Console.WriteLine("There is a high chance of stuff breaking");
            Console.WriteLine("Use at your own risk");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  UABEAvalonia batchexportbundle <directory>");
            Console.WriteLine("  UABEAvalonia batchimportbundle <directory>");
            Console.WriteLine("  UABEAvalonia applyemip <emip file> <directory>");
            Console.WriteLine("  UABEAvalonia patchrawasset <assets file> <dat file> [file id] [output file]");
            Console.WriteLine("  UABEAvalonia patchdumpasset <assets file> <dump file> [file id] [type] [output file]");
            Console.WriteLine("  UABEAvalonia getassetinfo <assets file> <path id> [file id]");
            Console.WriteLine("  UABEAvalonia listassets <assets file> [output file]");
            Console.WriteLine("  UABEAvalonia compressbundle <bundle file> <output> <lz4|lzma>");
            Console.WriteLine("  UABEAvalonia decompressbundle <bundle file> <output>");
            Console.WriteLine("  UABEAvalonia searchasset <assets file> <search term>");
            Console.WriteLine("  UABEAvalonia unity3dinfo <unity3d file>");
            Console.WriteLine();
            Console.WriteLine("Bundle import/export arguments:");
            Console.WriteLine("  -keepnames writes out to the exact file name in the bundle.");
            Console.WriteLine("  -kd keep .decomp files.");
            Console.WriteLine("  -fd overwrite old .decomp files.");
            Console.WriteLine("  -md decompress into memory. Doesn't write .decomp files.");
            Console.WriteLine();
            Console.WriteLine("patchrawasset arguments:");
            Console.WriteLine("  <assets file>: Input assets file");
            Console.WriteLine("  <dat file>: Raw asset file (.dat)");
            Console.WriteLine("  [file id]: (optional) File ID, default: 0");
            Console.WriteLine("  [output file]: (optional) Output file name");
            Console.WriteLine("      If not specified: saves as <assets file>.patch");
            Console.WriteLine("      If 'overwrite': replaces original file");
            Console.WriteLine();
            Console.WriteLine("patchdumpasset arguments:");
            Console.WriteLine("  <assets file>: Input assets file");
            Console.WriteLine("  <dump file>: Input dump file (txt/json)");
            Console.WriteLine("  [file id]: (optional) File ID, default: 0");
            Console.WriteLine("  [type]: (optional) 'txt', 'json' or 'auto'");
            Console.WriteLine("  [output file]: (optional) Output file name");
            Console.WriteLine("      If not specified: saves as <assets file>.patch");
            Console.WriteLine("      If 'overwrite': replaces original file");
        }

        // ================================================================
        //  PATCH RAW ASSET
        // ================================================================
        private static void PatchRawAsset(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: patchrawasset <assets file> <dat file> [file id] [output file]");
                return;
            }

            string fileToPatch = args[1];
            string datFile     = args[2];
            int    fileId      = 0;
            string outputFile  = fileToPatch + ".patch";

            if (args.Length >= 4)
            {
                if (int.TryParse(args[3], out fileId))
                {
                    if (args.Length >= 5)
                        outputFile = args[4];
                }
                else
                {
                    outputFile = args[3];
                }
            }

            if (!File.Exists(fileToPatch))
            {
                Console.WriteLine($"File {fileToPatch} does not exist!");
                return;
            }
            if (!File.Exists(datFile))
            {
                Console.WriteLine($"File {datFile} does not exist!");
                return;
            }

            // Parse pathId from the dat filename:  "SomeName-bundle.assets-123456.dat"
            string datNoExt = Path.GetFileNameWithoutExtension(datFile);
            int dashIdx = datNoExt.LastIndexOf('-');
            if (dashIdx < 0)
            {
                Console.WriteLine("Dat file name must contain pathID after the last '-'");
                Console.WriteLine("Example: AssetName-bundle.assets-123456.dat");
                return;
            }
            if (!long.TryParse(datNoExt[(dashIdx + 1)..], out long pathId))
            {
                Console.WriteLine($"Could not parse pathID from filename '{datNoExt}'");
                return;
            }

            try
            {
                byte[] newData = File.ReadAllBytes(datFile);
                Console.WriteLine($"Patching asset PathID={pathId} FileID={fileId}...");

                bool   overwrite = outputFile.ToLower() == "overwrite";
                string outPath   = overwrite ? fileToPatch + ".tmp" : outputFile;

                // FIX: single-pass — open source, read, write patched version
                using (FileStream inFs = File.OpenRead(fileToPatch))
                using (AssetsFileReader reader = new AssetsFileReader(inFs))
                {
                    AssetsFile afile = new AssetsFile();
                    afile.Read(reader);

                    AssetFileInfo? info = afile.GetAssetInfo(pathId);
                    if (info == null)
                    {
                        Console.WriteLine($"Asset PathID={pathId} not found.");
                        return;
                    }

                    // FIX: use GetScriptIndex() for the correct monoId
                    ushort monoId = afile.GetScriptIndex(info);

                    var replacer = new AssetsReplacerFromMemory(
                        pathId, info.TypeId, monoId, newData);

                    using (FileStream outFs = File.Open(outPath, FileMode.Create))
                    using (AssetsFileWriter writer = new AssetsFileWriter(outFs))
                    {
                        // FIX: pass 0 for format version — AssetsFile.Write uses the
                        //      header version it already has internally when you pass 0.
                        afile.Write(writer, 0, new List<AssetsReplacer> { replacer });
                    }
                }

                if (overwrite)
                {
                    File.Delete(fileToPatch);
                    File.Move(outPath, fileToPatch);
                    Console.WriteLine($"Overwrote original: {fileToPatch}");
                }
                else
                {
                    Console.WriteLine($"Saved patch to: {outputFile}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        // ================================================================
        //  PATCH DUMP ASSET
        // ================================================================
        private static void PatchDumpAsset(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: patchdumpasset <assets file> <dump file> [file id] [type] [output file]");
                return;
            }

            string fileToPatch = args[1];
            string dumpFile    = args[2];
            int    fileId      = 0;
            string dumpType    = "auto";
            string outputFile  = fileToPatch + ".patch";

            int argIdx = 3;
            if (argIdx < args.Length && int.TryParse(args[argIdx], out fileId))
                argIdx++;

            if (argIdx < args.Length)
            {
                string t = args[argIdx].ToLower();
                if (t == "txt" || t == "json" || t == "auto")
                {
                    dumpType = t;
                    argIdx++;
                }
            }

            if (argIdx < args.Length)
                outputFile = args[argIdx];

            if (!File.Exists(fileToPatch))
            {
                Console.WriteLine($"File {fileToPatch} does not exist!");
                return;
            }
            if (!File.Exists(dumpFile))
            {
                Console.WriteLine($"File {dumpFile} does not exist!");
                return;
            }

            // Parse pathId from dump filename
            string dumpNoExt = Path.GetFileNameWithoutExtension(dumpFile);
            int di = dumpNoExt.LastIndexOf('-');
            if (di < 0)
            {
                Console.WriteLine("Dump file name must contain pathID after the last '-'");
                Console.WriteLine("Example: AssetName-bundle.assets-123456.txt");
                return;
            }
            if (!long.TryParse(dumpNoExt[(di + 1)..], out long pathId))
            {
                Console.WriteLine($"Could not parse pathID from filename '{dumpNoExt}'");
                return;
            }

            if (dumpType == "auto")
                dumpType = dumpFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    ? "json" : "txt";

            Console.WriteLine($"Importing {dumpType} dump, PathID={pathId} FileID={fileId}...");

            try
            {
                // FIX: one AssetsManager, one load — keep the file open throughout
                //      so we can get the template field AND write the patched output.
                var manager = new AssetsManager();

                // FIX: load classdata BEFORE loading the assets file
                string classDataPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
                if (File.Exists(classDataPath))
                    manager.LoadClassPackage(classDataPath);

                AssetsFileInstance afileInst = manager.LoadAssetsFile(fileToPatch, false);
                AssetsFile         afile     = afileInst.file;

                // Load class DB so template fields resolve even without type-tree
                string unityVer = afile.Metadata.UnityVersion;
                if (unityVer == "0.0.0" && afileInst.parentBundle != null)
                    unityVer = afileInst.parentBundle.file.Header.EngineVersion;
                if (File.Exists(classDataPath))
                    manager.LoadClassDatabaseFromPackage(unityVer);

                AssetFileInfo? info = afile.GetAssetInfo(pathId);
                if (info == null)
                {
                    Console.WriteLine($"Asset PathID={pathId} not found.");
                    manager.UnloadAllAssetsFiles(true);
                    return;
                }

                // --- import the dump into raw bytes ---
                byte[]? newData = ImportDump(manager, afileInst, info, dumpFile, dumpType);
                if (newData == null)
                {
                    // ImportDump already printed the error message
                    manager.UnloadAllAssetsFiles(true);
                    return;
                }

                // FIX: correct monoId from GetScriptIndex, not TypeIdOrIndex
                ushort monoId = afile.GetScriptIndex(info);

                var replacer = new AssetsReplacerFromMemory(
                    pathId, info.TypeId, monoId, newData);

                bool   overwrite = outputFile.ToLower() == "overwrite";
                string outPath   = overwrite ? fileToPatch + ".tmp" : outputFile;

                // Write the patched file while the source is still open
                using (FileStream outFs = File.Open(outPath, FileMode.Create))
                using (AssetsFileWriter writer = new AssetsFileWriter(outFs))
                {
                    afile.Write(writer, 0, new List<AssetsReplacer> { replacer });
                }

                // Now we can safely close the source
                manager.UnloadAllAssetsFiles(true);

                if (overwrite)
                {
                    File.Delete(fileToPatch);
                    File.Move(outPath, fileToPatch);
                    Console.WriteLine($"Overwrote original: {fileToPatch}");
                }
                else
                {
                    Console.WriteLine($"Saved patch to: {outputFile}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads a dump file (txt or json) and returns the imported raw asset bytes,
        /// or null if the import failed (error already printed to console).
        /// </summary>
        private static byte[]? ImportDump(
            AssetsManager      manager,
            AssetsFileInstance afileInst,
            AssetFileInfo      info,
            string             dumpFile,
            string             dumpType)   // "txt" or "json" (never "auto" here)
        {
            var importer = new AssetImportExport();

            using FileStream   dumpFs = File.OpenRead(dumpFile);
            using StreamReader sr     = new StreamReader(dumpFs);

            if (dumpType == "json")
            {
                AssetTypeTemplateField? templateField = null;
                try
                {
                    // GetBaseField deserializes the asset so we can obtain its schema
                    AssetTypeValueField baseField = manager.GetBaseField(afileInst, info);
                    templateField = baseField.TemplateField;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: could not get template field for JSON import: {ex.Message}");
                    Console.WriteLine("Falling back to text import...");
                    dumpType = "txt";
                }

                if (templateField != null)
                {
                    // FIX: ImportJsonAsset needs the template field, not the value field
                    byte[]? data = importer.ImportJsonAsset(templateField, sr, out string? errMsg);
                    if (data == null)
                    {
                        Console.WriteLine($"JSON import failed: {errMsg}");
                        return null;
                    }
                    return data;
                }
            }

            // txt import (or fallback from failed json)
            if (dumpType == "txt")
            {
                // Rewind in case we read something during the json path
                sr.BaseStream.Position = 0;
                sr.DiscardBufferedData();

                byte[]? data = importer.ImportTextAsset(sr, out string? errMsg);
                if (data == null)
                {
                    Console.WriteLine($"Text import failed: {errMsg}");
                    return null;
                }
                return data;
            }

            return null;
        }

        // ================================================================
        //  GET ASSET INFO
        // ================================================================
        private static void GetAssetInfo(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: getassetinfo <assets file> <path id> [file id]");
                return;
            }

            string assetsFile = args[1];
            if (!long.TryParse(args[2], out long pathId))
            {
                Console.WriteLine("Invalid path ID");
                return;
            }

            int fileId = 0;
            if (args.Length >= 4 && !int.TryParse(args[3], out fileId))
            {
                Console.WriteLine("Invalid file ID");
                return;
            }

            if (!File.Exists(assetsFile))
            {
                Console.WriteLine($"File {assetsFile} does not exist!");
                return;
            }

            var manager = new AssetsManager();

            string classDataPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
            if (File.Exists(classDataPath))
                manager.LoadClassPackage(classDataPath);

            try
            {
                var        inst = manager.LoadAssetsFile(assetsFile, false);
                AssetsFile file = inst.file;

                if (File.Exists(classDataPath))
                    manager.LoadClassDatabaseFromPackage(file.Metadata.UnityVersion);

                AssetFileInfo? info = file.GetAssetInfo(pathId);
                if (info == null)
                {
                    Console.WriteLine($"Asset PathID={pathId} not found.");
                    return;
                }

                string typeName;
                try
                {
                    var tmpl = manager.GetTemplateBaseField(inst, info);
                    typeName = (!string.IsNullOrEmpty(tmpl?.Type))
                        ? tmpl.Type : GetCommonTypeName(info.TypeId);
                }
                catch
                {
                    typeName = GetCommonTypeName(info.TypeId);
                }

                // FIX: use GetScriptIndex for monoId
                ushort monoId = file.GetScriptIndex(info);

                Console.WriteLine("Asset Information:");
                Console.WriteLine($"  File ID      : {fileId}");
                Console.WriteLine($"  Path ID      : {info.PathId}");
                Console.WriteLine($"  Type Name    : {typeName}");
                Console.WriteLine($"  Type ID      : 0x{info.TypeId:X8}");
                Console.WriteLine($"  Byte Size    : {info.ByteSize} bytes");
                Console.WriteLine($"  Byte Start   : 0x{info.AbsoluteByteStart:X}");
                Console.WriteLine($"  Type Tree    : {file.Metadata.TypeTreeEnabled}");
                if (monoId != 0xFFFF)
                    Console.WriteLine($"  Script Index : {monoId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                manager.UnloadAllAssetsFiles(true);
            }
        }

        // ================================================================
        //  LIST ASSETS
        // ================================================================
        private static void ListAssets(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: listassets <assets file> [output file]");
                return;
            }

            string  assetsFile = args[1];
            string? outputFile = args.Length >= 3 ? args[2] : null;

            if (!File.Exists(assetsFile))
            {
                Console.WriteLine($"File {assetsFile} does not exist!");
                return;
            }

            var manager = new AssetsManager();

            // FIX: load classdata BEFORE loading the assets file
            string classDataPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
            if (File.Exists(classDataPath))
                manager.LoadClassPackage(classDataPath);

            try
            {
                var        inst = manager.LoadAssetsFile(assetsFile, false);
                AssetsFile file = inst.file;

                string unityVer = file.Metadata.UnityVersion;
                if (unityVer == "0.0.0" && inst.parentBundle != null)
                    unityVer = inst.parentBundle.file.Header.EngineVersion;

                if (File.Exists(classDataPath))
                    manager.LoadClassDatabaseFromPackage(unityVer);

                var sb = new StringBuilder();
                sb.AppendLine($"Assets in : {Path.GetFileName(assetsFile)}");
                sb.AppendLine($"Unity Ver : {unityVer}");
                sb.AppendLine($"Count     : {file.AssetInfos.Count}");
                sb.AppendLine($"Type Tree : {file.Metadata.TypeTreeEnabled}");
                sb.AppendLine();
                sb.AppendLine($"{"PathID",12} | {"TypeID",10} | {"Size",9} | {"Type",-24} | Name");
                sb.AppendLine(new string('-', 95));

                foreach (var info in file.AssetInfos)
                {
                    string typeName  = ResolveTypeName(manager, inst, file, info);
                    string assetName = ResolveAssetName(manager, inst, info, typeName);
                    sb.AppendLine(
                        $"{info.PathId,12} | 0x{info.TypeId:X8} | {info.ByteSize,9} | {typeName,-24} | {assetName}");
                }

                string result = sb.ToString();
                if (outputFile != null)
                {
                    File.WriteAllText(outputFile, result);
                    Console.WriteLine($"Saved to: {outputFile}");
                }
                else
                {
                    Console.Write(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                manager.UnloadAllAssetsFiles(true);
            }
        }

        // ================================================================
        //  SEARCH ASSET
        // ================================================================
        private static void SearchAsset(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: searchasset <assets file> <search term>");
                return;
            }

            string assetsFile  = args[1];
            string searchTerm  = args[2];

            if (!File.Exists(assetsFile))
            {
                Console.WriteLine($"File {assetsFile} does not exist!");
                return;
            }

            var manager = new AssetsManager();

            string classDataPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
            if (File.Exists(classDataPath))
                manager.LoadClassPackage(classDataPath);

            try
            {
                var        inst = manager.LoadAssetsFile(assetsFile, false);
                AssetsFile file = inst.file;

                string unityVer = file.Metadata.UnityVersion;
                if (unityVer == "0.0.0" && inst.parentBundle != null)
                    unityVer = inst.parentBundle.file.Header.EngineVersion;

                if (File.Exists(classDataPath))
                    manager.LoadClassDatabaseFromPackage(unityVer);

                Console.WriteLine($"Searching for '{searchTerm}' in {Path.GetFileName(assetsFile)}...");
                Console.WriteLine();

                bool found = false;
                foreach (var info in file.AssetInfos)
                {
                    string typeName  = ResolveTypeName(manager, inst, file, info);
                    string assetName = ResolveAssetName(manager, inst, info, typeName);

                    if (assetName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        typeName .IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Console.WriteLine(
                            $"PathID={info.PathId,-12} Size={info.ByteSize,9} " +
                            $"Type={typeName,-24} Name={assetName}");
                        found = true;
                    }
                }

                if (!found)
                    Console.WriteLine("No matching assets found.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                manager.UnloadAllAssetsFiles(true);
            }
        }

        // ================================================================
        //  UNITY3D INFO
        // ================================================================
        private static void Unity3dInfo(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: unity3dinfo <unity3d file>");
                return;
            }

            string unity3dFile = args[1];
            if (!File.Exists(unity3dFile))
            {
                Console.WriteLine($"File {unity3dFile} does not exist!");
                return;
            }

            // Try as plain assets file first
            try
            {
                var manager = new AssetsManager();
                var inst    = manager.LoadAssetsFile(unity3dFile, false);
                Console.WriteLine("Type: AssetsFile");
                Console.WriteLine($"  Name         : {Path.GetFileName(unity3dFile)}");
                Console.WriteLine($"  Size         : {new FileInfo(unity3dFile).Length} bytes");
                Console.WriteLine($"  Unity Version: {inst.file.Metadata.UnityVersion}");
                Console.WriteLine($"  Asset Count  : {inst.file.AssetInfos.Count}");
                Console.WriteLine($"  Type Tree    : {inst.file.Metadata.TypeTreeEnabled}");
                manager.UnloadAllAssetsFiles(true);
                return;
            }
            catch { }

            // Try as bundle
            try
            {
                AssetBundleFile bun = new AssetBundleFile();
                using FileStream       fs = File.OpenRead(unity3dFile);
                using AssetsFileReader r  = new AssetsFileReader(fs);
                bun.Read(r);

                Console.WriteLine("Type: Bundle");
                Console.WriteLine($"  Name             : {Path.GetFileName(unity3dFile)}");
                Console.WriteLine($"  Size             : {new FileInfo(unity3dFile).Length} bytes");
                Console.WriteLine($"  Engine Version   : {bun.Header.EngineVersion}");
                Console.WriteLine($"  Compression Type : {bun.Header.GetCompressionType()}");
                Console.WriteLine($"  Directory Count  : {bun.BlockAndDirInfo.DirectoryInfos.Length}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse file: {ex.Message}");
            }
        }

        // ================================================================
        //  COMPRESS / DECOMPRESS BUNDLE
        // ================================================================
        private static void CompressBundle(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: compressbundle <bundle file> <output> <lz4|lzma>");
                return;
            }

            string bundleFile  = args[1];
            string outputFile  = args[2];
            string compStr     = args[3].ToLower();

            AssetBundleCompressionType compType = compStr switch
            {
                "lz4"  => AssetBundleCompressionType.LZ4,
                "lzma" => AssetBundleCompressionType.LZMA,
                _      => AssetBundleCompressionType.None
            };

            if (compType == AssetBundleCompressionType.None)
            {
                Console.WriteLine("Invalid compression type. Use 'lz4' or 'lzma'.");
                return;
            }

            if (!File.Exists(bundleFile))
            {
                Console.WriteLine($"File {bundleFile} does not exist!");
                return;
            }

            try
            {
                AssetBundleFile bun = new AssetBundleFile();
                using (FileStream       inFs   = File.OpenRead(bundleFile))
                using (AssetsFileReader reader = new AssetsFileReader(inFs))
                {
                    bun.Read(reader);
                    using FileStream       outFs  = File.Open(outputFile, FileMode.Create);
                    using AssetsFileWriter writer = new AssetsFileWriter(outFs);
                    bun.Pack(reader, writer, compType, true);
                }
                Console.WriteLine($"Compressed to {outputFile} ({compStr.ToUpper()})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static void DecompressBundle(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: decompressbundle <bundle file> <output>");
                return;
            }

            string bundleFile = args[1];
            string outputFile = args[2];

            if (!File.Exists(bundleFile))
            {
                Console.WriteLine($"File {bundleFile} does not exist!");
                return;
            }

            try
            {
                AssetBundleFile bun = new AssetBundleFile();
                using (FileStream       inFs   = File.OpenRead(bundleFile))
                using (AssetsFileReader reader = new AssetsFileReader(inFs))
                {
                    bun.Read(reader);
                    using FileStream       outFs  = File.Open(outputFile, FileMode.Create);
                    using AssetsFileWriter writer = new AssetsFileWriter(outFs);
                    bun.Unpack(writer);
                }
                Console.WriteLine($"Decompressed to {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        // ================================================================
        //  Shared name / type resolution helpers
        // ================================================================

        private static string ResolveTypeName(
            AssetsManager manager, AssetsFileInstance inst,
            AssetsFile file, AssetFileInfo info)
        {
            if (file.Metadata.TypeTreeEnabled)
            {
                try
                {
                    var tmpl = manager.GetTemplateBaseField(inst, info);
                    if (tmpl != null && !string.IsNullOrEmpty(tmpl.Type))
                        return tmpl.Type;
                }
                catch { }
            }
            return GetCommonTypeName(info.TypeId);
        }

        private static string ResolveAssetName(
            AssetsManager manager, AssetsFileInstance inst,
            AssetFileInfo info, string typeName)
        {
            try
            {
                // FIX: GetBaseField can throw — catch and fall back gracefully
                AssetTypeValueField bf = manager.GetBaseField(inst, info);
                if (bf == null) return typeName;

                // Standard Unity name fields
                foreach (string fn in new[] { "m_Name", "name", "Name" })
                {
                    var f = bf[fn];
                    if (f != null && !f.IsDummy &&
                        f.Value?.ValueType == AssetValueType.String)
                    {
                        string n = f.AsString;
                        if (!string.IsNullOrWhiteSpace(n) && n != "null" && n != "None")
                            return n;
                    }
                }

                // MonoBehaviour: show the script class name as a hint
                if (typeName == "MonoBehaviour")
                {
                    var sf = bf["m_Script"];
                    if (sf != null && !sf.IsDummy)
                    {
                        var cf = sf["m_ClassName"];
                        if (cf != null && !cf.IsDummy && cf.Value?.ValueType == AssetValueType.String)
                            return cf.AsString + " (MB)";
                    }
                }
            }
            catch { }

            return typeName;
        }

        // ================================================================
        //  Bundle batch helpers
        // ================================================================

        private static string GetMainFileName(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
                if (!args[i].StartsWith("-")) return args[i];
            return string.Empty;
        }

        private static HashSet<string> GetFlags(string[] args)
        {
            var flags = new HashSet<string>();
            for (int i = 1; i < args.Length; i++)
                if (args[i].StartsWith("-")) flags.Add(args[i]);
            return flags;
        }

        /// <summary>
        /// Opens a bundle, decompresses if needed, and returns the AssetBundleFile.
        /// Caller must call Close() when finished.
        /// </summary>
        private static AssetBundleFile DecompressBundle(string file, string? decompFile)
        {
            AssetBundleFile  bun = new AssetBundleFile();
            Stream           fs  = File.OpenRead(file);
            AssetsFileReader r   = new AssetsFileReader(fs);
            bun.Read(r);

            if (bun.Header.GetCompressionType() != 0)
            {
                Stream nfs = decompFile == null
                    ? (Stream)new MemoryStream()
                    : File.Open(decompFile, FileMode.Create, FileAccess.ReadWrite);

                AssetsFileWriter w = new AssetsFileWriter(nfs);
                bun.Unpack(w);

                nfs.Position = 0;
                fs.Close();

                bun = new AssetBundleFile();
                bun.Read(new AssetsFileReader(nfs));
            }

            return bun;
        }

        private static string? GetNextBackup(string path)
        {
            for (int i = 0; i < 10000; i++)
            {
                string bak = $"{path}.bak{i.ToString().PadLeft(4, '0')}";
                if (!File.Exists(bak)) return bak;
            }
            Console.WriteLine("Too many backups, exiting for your safety.");
            return null;
        }

        private static void BatchExportBundle(string[] args)
        {
            string dir = GetMainFileName(args);
            if (!Directory.Exists(dir))
            {
                Console.WriteLine("Directory does not exist!");
                return;
            }

            var flags = GetFlags(args);
            foreach (string file in Directory.EnumerateFiles(dir))
            {
                if (FileTypeDetector.DetectFileType(file) != DetectedFileType.BundleFile)
                    continue;

                string? decompFile = flags.Contains("-md") ? null : $"{file}.decomp";
                Console.WriteLine($"Decompressing {file}...");
                AssetBundleFile bun = DecompressBundle(file, decompFile);

                int count = bun.BlockAndDirInfo.DirectoryInfos.Length;
                for (int i = 0; i < count; i++)
                {
                    string name = bun.BlockAndDirInfo.DirectoryInfos[i].Name;
                    byte[] data = BundleHelper.LoadAssetDataFromBundle(bun, i);
                    string outName = flags.Contains("-keepnames")
                        ? Path.Combine(dir, name)
                        : Path.Combine(dir, $"{Path.GetFileName(file)}_{name}");
                    Console.WriteLine($"  Exporting {outName}...");
                    File.WriteAllBytes(outName, data);
                }

                bun.Close();

                if (!flags.Contains("-kd") && !flags.Contains("-md") &&
                    decompFile != null && File.Exists(decompFile))
                    File.Delete(decompFile);

                Console.WriteLine("Done.");
            }
        }

        private static void BatchImportBundle(string[] args)
        {
            string dir = GetMainFileName(args);
            if (!Directory.Exists(dir))
            {
                Console.WriteLine("Directory does not exist!");
                return;
            }

            var flags = GetFlags(args);
            foreach (string file in Directory.EnumerateFiles(dir))
            {
                if (FileTypeDetector.DetectFileType(file) != DetectedFileType.BundleFile)
                    continue;

                string? decompFile = flags.Contains("-md") ? null : $"{file}.decomp";
                Console.WriteLine($"Decompressing {file} to {(decompFile ?? "memory")}...");
                AssetBundleFile bun = DecompressBundle(file, decompFile);

                var reps    = new List<BundleReplacer>();
                var streams = new List<Stream>();

                int count = bun.BlockAndDirInfo.DirectoryInfos.Length;
                for (int i = 0; i < count; i++)
                {
                    string name  = bun.BlockAndDirInfo.DirectoryInfos[i].Name;
                    string match = Path.Combine(dir, $"{Path.GetFileName(file)}_{name}");
                    if (File.Exists(match))
                    {
                        FileStream fs = File.OpenRead(match);
                        reps.Add(new BundleReplacerFromStream(name, name, true, fs, 0, fs.Length));
                        streams.Add(fs);
                        Console.WriteLine($"  Importing {match}...");
                    }
                }

                byte[] outData;
                using (MemoryStream    ms = new MemoryStream())
                using (AssetsFileWriter w = new AssetsFileWriter(ms))
                {
                    bun.Write(w, reps);
                    outData = ms.ToArray();
                }

                Console.WriteLine($"Writing {file}...");
                foreach (Stream s in streams) s.Close();
                bun.Close();
                File.WriteAllBytes(file, outData);

                if (!flags.Contains("-kd") && !flags.Contains("-md") &&
                    decompFile != null && File.Exists(decompFile))
                    File.Delete(decompFile);

                Console.WriteLine("Done.");
            }
        }

        private static void ApplyEmip(string[] args)
        {
            var flags = GetFlags(args);
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: applyemip <emip file> <directory>");
                return;
            }

            string emipFile = args[1];
            string rootDir  = args[2];

            if (!File.Exists(emipFile))
            {
                Console.WriteLine($"File {emipFile} does not exist!");
                return;
            }

            var instPkg = new InstallerPackageFile();
            using (FileStream       fs = File.OpenRead(emipFile))
            using (AssetsFileReader r  = new AssetsFileReader(fs))
            {
                instPkg.Read(r, true);
            }

            Console.WriteLine($"Installing: {instPkg.modName} by {instPkg.modCreators}");
            Console.WriteLine(instPkg.modDescription);

            foreach (var affectedFile in instPkg.affectedFiles)
            {
                string affectedFileName = Path.GetFileName(affectedFile.path);
                string affectedFilePath = Path.Combine(rootDir, affectedFile.path);

                if (affectedFile.isBundle)
                {
                    string? decompFile = flags.Contains("-md") ? null : $"{affectedFilePath}.decomp";
                    string  modFile    = $"{affectedFilePath}.mod";
                    string? bakFile    = GetNextBackup(affectedFilePath);
                    if (bakFile == null) return;

                    Console.WriteLine($"Decompressing {affectedFileName} to {decompFile ?? "memory"}...");
                    AssetBundleFile bun  = DecompressBundle(affectedFilePath, decompFile);
                    var             reps = new List<BundleReplacer>();

                    foreach (var rep in affectedFile.replacers)
                    {
                        var bunRep = (BundleReplacer)rep;
                        if (bunRep is BundleReplacerFromAssets)
                        {
                            string  entryName = bunRep.GetOriginalEntryName();
                            var     dirInfo   = BundleHelper.GetDirInfo(bun, entryName);
                            bunRep.Init(bun.DataReader, dirInfo.Offset, dirInfo.DecompressedSize);
                        }
                        reps.Add(bunRep);
                    }

                    Console.WriteLine($"Writing {modFile}...");
                    using (FileStream       mfs = File.Open(modFile, FileMode.Create))
                    using (AssetsFileWriter mw  = new AssetsFileWriter(mfs))
                    {
                        bun.Write(mw, reps, instPkg.addedTypes);
                    }
                    bun.Close();

                    Console.WriteLine("Swapping mod file...");
                    File.Move(affectedFilePath, bakFile);
                    File.Move(modFile, affectedFilePath);

                    if (!flags.Contains("-kd") && !flags.Contains("-md") &&
                        decompFile != null && File.Exists(decompFile))
                        File.Delete(decompFile);

                    Console.WriteLine("Done.");
                }
                else
                {
                    string  modFile = $"{affectedFilePath}.mod";
                    string? bakFile = GetNextBackup(affectedFilePath);
                    if (bakFile == null) return;

                    using (FileStream       afs = File.OpenRead(affectedFilePath))
                    using (AssetsFileReader ar  = new AssetsFileReader(afs))
                    {
                        AssetsFile assets = new AssetsFile();
                        assets.Read(ar);

                        var reps = new List<AssetsReplacer>();
                        foreach (var rep in affectedFile.replacers)
                            reps.Add((AssetsReplacer)rep);

                        Console.WriteLine($"Writing {modFile}...");
                        using (FileStream       mfs = File.Open(modFile, FileMode.Create))
                        using (AssetsFileWriter mw  = new AssetsFileWriter(mfs))
                        {
                            assets.Write(mw, 0, reps, instPkg.addedTypes);
                        }
                    }

                    Console.WriteLine("Swapping mod file...");
                    File.Move(affectedFilePath, bakFile);
                    File.Move(modFile, affectedFilePath);
                    Console.WriteLine("Done.");
                }
            }
        }

        // ================================================================
        //  Common Unity type-ID → name table
        // ================================================================
        private static string GetCommonTypeName(int typeId)
        {
            return typeId switch
            {
                1   => "GameObject",
                2   => "Component",
                4   => "Transform",
                20  => "Camera",
                21  => "Material",
                23  => "MeshRenderer",
                25  => "Renderer",
                28  => "Texture",
                29  => "Texture2D",
                30  => "RenderTexture",
                31  => "Mesh",
                32  => "Shader",
                33  => "TextAsset",
                37  => "AssetBundle",
                40  => "MonoBehaviour",
                41  => "MonoScript",
                43  => "Font",
                48  => "SphereCollider",
                49  => "CapsuleCollider",
                50  => "BoxCollider",
                54  => "Rigidbody",
                64  => "AnimationClip",
                74  => "AudioListener",
                75  => "AudioSource",
                76  => "AudioClip",
                82  => "MeshFilter",
                84  => "Canvas",
                85  => "RectTransform",
                91  => "Avatar",
                93  => "Animator",
                102 => "TextMesh",
                108 => "Light",
                111 => "Animation",
                114 => "MonoManager",
                115 => "Texture2DArray",
                119 => "RectTransform",
                128 => "PlayerSettings",
                135 => "SpriteAtlas",
                213 => "Sprite",
                221 => "AnimatorController",
                258 => "NavMeshData",
                298 => "ParticleSystem",
                _   => $"TypeID_{typeId}"
            };
        }

        // ================================================================
        //  Main entry
        // ================================================================
        public static void CLHMain(string[] args)
        {
            if (args.Length < 1)
            {
                PrintHelp();
                return;
            }

            string command = args[0].ToLower();

            try
            {
                switch (command)
                {
                    case "batchexportbundle": BatchExportBundle(args); break;
                    case "batchimportbundle": BatchImportBundle(args); break;
                    case "applyemip":         ApplyEmip(args);         break;
                    case "patchrawasset":     PatchRawAsset(args);     break;
                    case "patchdumpasset":    PatchDumpAsset(args);    break;
                    case "getassetinfo":      GetAssetInfo(args);      break;
                    case "listassets":        ListAssets(args);        break;
                    case "compressbundle":    CompressBundle(args);    break;
                    case "decompressbundle":  DecompressBundle(args);  break;
                    case "searchasset":       SearchAsset(args);       break;
                    case "unity3dinfo":       Unity3dInfo(args);       break;
                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        PrintHelp();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled error: {ex.Message}");
            }
        }
    }
}
