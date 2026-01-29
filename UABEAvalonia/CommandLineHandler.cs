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
            Console.WriteLine("      Normally, file names are prepended with the bundle's name.");
            Console.WriteLine("      Note: these names are not compatible with batchimport.");
            Console.WriteLine("  -kd keep .decomp files. When UABEA opens compressed bundles,");
            Console.WriteLine("      they are decompressed into .decomp files. If you want to");
            Console.WriteLine("      decompress bundles, you can use this flag to keep them");
            Console.WriteLine("      without deleting them.");
            Console.WriteLine("  -fd overwrite old .decomp files.");
            Console.WriteLine("  -md decompress into memory. Doesn't write .decomp files.");
            Console.WriteLine("      -kd and -fd won't do anything with this flag set.");
            Console.WriteLine();
            Console.WriteLine("patchrawasset arguments:");
            Console.WriteLine("  <assets file>: Input assets file (can be .assets or .unity3d)");
            Console.WriteLine("  <dat file>: Raw asset file (.dat)");
            Console.WriteLine("  [file id]: (optional) File ID if multiple files (default: 0)");
            Console.WriteLine("  [output file]: (optional) Output file name");
            Console.WriteLine("      If not specified: saves as <assets file>.patch");
            Console.WriteLine("      If 'overwrite': replaces original file");
            Console.WriteLine();
            Console.WriteLine("patchdumpasset arguments:");
            Console.WriteLine("  <assets file>: Input assets file (can be .assets or .unity3d)");
            Console.WriteLine("  <dump file>: Input dump file (txt/json)");
            Console.WriteLine("  [file id]: (optional) File ID if multiple files (default: 0)");
            Console.WriteLine("  [type]: (optional) 'txt', 'json' or 'auto' (default: auto-detect from extension)");
            Console.WriteLine("  [output file]: (optional) Output file name");
            Console.WriteLine("      If not specified: saves as <assets file>.patch");
            Console.WriteLine("      If 'overwrite': replaces original file");
            Console.WriteLine();
            Console.WriteLine("getassetinfo arguments:");
            Console.WriteLine("  <assets file>: Input assets file (can be .assets or .unity3d)");
            Console.WriteLine("  <path id>: Path ID of the asset to get info about");
            Console.WriteLine("  [file id]: (optional) File ID if multiple files (default: 0)");
            Console.WriteLine();
            Console.WriteLine("listassets arguments:");
            Console.WriteLine("  <assets file>: Input assets file (can be .assets or .unity3d)");
            Console.WriteLine("  [output file]: (optional) Output text file name");
            Console.WriteLine("      If not specified: prints to console");
        }

        // ---------------- PATCH RAW ASSET ----------------
        private static void PatchRawAsset(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: patchrawasset <assets file> <dat file> [file id] [output file]");
                Console.WriteLine("  <assets file>: Input assets file (can be .assets or .unity3d)");
                Console.WriteLine("  <dat file>: Raw asset file (.dat)");
                Console.WriteLine("  [file id]: (optional) File ID if multiple files (default: 0)");
                Console.WriteLine("  [output file]: (optional) Output file name");
                Console.WriteLine("      If not specified: saves as <assets file>.patch");
                return;
            }

            string fileToPatch = args[1];
            string datFile = args[2];
            
            // پارامترهای اختیاری
            int fileId = 0;
            string outputFile = fileToPatch + ".patch";
            
            if (args.Length >= 4)
            {
                // چک کن اگر آرگومان بعدی File ID است یا Output File
                if (int.TryParse(args[3], out fileId))
                {
                    if (args.Length >= 5)
                    {
                        outputFile = args[4];
                    }
                }
                else
                {
                    // آرگومان سوم Output File است
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

            try
            {
                long datFilePathId;
                byte[] bytes;
                AssetsFile afile;
                
                var manager = new AssetsManager();
                try
                {
                    var afileInst = manager.LoadAssetsFile(fileToPatch, false);
                    afile = afileInst.file;

                    var datFileNoExt = Path.GetFileNameWithoutExtension(datFile);
                    int dashIdx = datFileNoExt.LastIndexOf('-');
                    if (dashIdx < 0)
                    {
                        Console.WriteLine("Dat file name must contain pathID after the last '-'");
                        Console.WriteLine("Example: AssetName-bundle.assets-123456.dat");
                        return;
                    }
                    var datFilePathIdStr = datFileNoExt[(dashIdx + 1)..];
                    if (!long.TryParse(datFilePathIdStr, out datFilePathId))
                    {
                        Console.WriteLine($"Could not parse pathID '{datFilePathIdStr}'");
                        return;
                    }

                    Console.WriteLine($"Patching asset with File ID: {fileId}, Path ID: {datFilePathId}");
                    bytes = File.ReadAllBytes(datFile);
                }
                finally
                {
                    manager.UnloadAllAssetsFiles(true);
                }

                using (FileStream fs = File.OpenRead(fileToPatch))
                using (AssetsFileReader reader = new AssetsFileReader(fs))
                {
                    afile = new AssetsFile();
                    afile.Read(reader);
                    
                    var assetInfo = afile.GetAssetInfo(datFilePathId);
                    if (assetInfo == null)
                    {
                        Console.WriteLine($"Asset with Path ID {datFilePathId} not found in File ID {fileId}");
                        return;
                    }

                    var replacer = new AssetsReplacerFromMemory(datFilePathId, assetInfo.TypeId, (ushort)assetInfo.TypeIdOrIndex, bytes);
                    List<AssetsReplacer> reps = new List<AssetsReplacer> { replacer };

                    string tempFile = outputFile;
                    bool overwriteOriginal = false;
                    
                    if (outputFile.ToLower() == "overwrite")
                    {
                        overwriteOriginal = true;
                        tempFile = fileToPatch + ".patch";
                    }

                    using (var writer = new AssetsFileWriter(tempFile))
                    {
                        afile.Write(writer, 0, reps, null);
                    }

                    if (overwriteOriginal)
                    {
                        fs.Close();
                        reader.Close();
                        
                        File.Delete(fileToPatch);
                        File.Move(tempFile, fileToPatch);
                        Console.WriteLine($"Patched asset (File ID: {fileId}, Path ID: {datFilePathId}) into {fileToPatch}");
                    }
                    else
                    {
                        Console.WriteLine($"Patched asset (File ID: {fileId}, Path ID: {datFilePathId}) into {outputFile}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return;
            }
        }

        // ---------------- PATCH DUMP ASSET ----------------
        private static void PatchDumpAsset(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: patchdumpasset <assets file> <dump file> [file id] [type] [output file]");
                Console.WriteLine("  <assets file>: Input assets file (can be .assets or .unity3d)");
                Console.WriteLine("  <dump file>: Dump file (txt/json)");
                Console.WriteLine("  [file id]: (optional) File ID if multiple files (default: 0)");
                Console.WriteLine("  [type]: (optional) 'txt', 'json' or 'auto' (default: auto-detect from extension)");
                Console.WriteLine("  [output file]: (optional) Output file name");
                Console.WriteLine("      If not specified: saves as <assets file>.patch");
                return;
            }

            string fileToPatch = args[1];
            string dumpFile = args[2];
            
            // پارامترهای اختیاری
            int fileId = 0;
            string dumpType = "auto";
            string outputFile = fileToPatch + ".patch";
            
            int argIndex = 3;
            
            // پارس File ID
            if (argIndex < args.Length && int.TryParse(args[argIndex], out fileId))
            {
                argIndex++;
            }
            
            // پارس Dump Type
            if (argIndex < args.Length)
            {
                string possibleType = args[argIndex].ToLower();
                if (possibleType == "txt" || possibleType == "json" || possibleType == "auto")
                {
                    dumpType = possibleType;
                    argIndex++;
                }
            }
            
            // پارس Output File
            if (argIndex < args.Length)
            {
                outputFile = args[argIndex];
            }

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

            try
            {
                long dumpFilePathId;
                byte[] bytes;
                AssetsFile afile;
                AssetFileInfo asset;
                
                var manager = new AssetsManager();
                try
                {
                    var afileInst = manager.LoadAssetsFile(fileToPatch, false);
                    afile = afileInst.file;

                    var dumpFileNoExt = Path.GetFileNameWithoutExtension(dumpFile);
                    int dashIdx = dumpFileNoExt.LastIndexOf('-');
                    if (dashIdx < 0)
                    {
                        Console.WriteLine("Dump file name must contain pathID after the last '-'");
                        Console.WriteLine("Example: AssetName-bundle.assets-123456.txt");
                        return;
                    }
                    var dumpFilePathIdStr = dumpFileNoExt[(dashIdx + 1)..];
                    if (!long.TryParse(dumpFilePathIdStr, out dumpFilePathId))
                    {
                        Console.WriteLine($"Could not parse pathID '{dumpFilePathIdStr}'");
                        return;
                    }

                    Console.WriteLine($"Patching asset with File ID: {fileId}, Path ID: {dumpFilePathId}");
                    
                    asset = afile.GetAssetInfo(dumpFilePathId);
                    if (asset == null)
                    {
                        Console.WriteLine($"Asset with pathID {dumpFilePathId} not found in File ID {fileId}");
                        return;
                    }

                    using (FileStream fs = File.OpenRead(dumpFile))
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        AssetImportExport importer = new AssetImportExport();
                        
                        if (dumpType == "auto")
                        {
                            if (dumpFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                dumpType = "json";
                            else
                                dumpType = "txt";
                        }

                        string? exceptionMessage = null;
                        
                        if (dumpType == "json")
                        {
                            AssetTypeValueField baseField = null;
                            
                            try
                            {
                                baseField = manager.GetBaseField(afileInst, asset);
                            }
                            catch
                            {
                                Console.WriteLine($"Warning: Could not deserialize asset {dumpFilePathId} for JSON import");
                                Console.WriteLine("Attempting to create minimal template...");
                            }
                            
                            if (baseField != null)
                            {
                                bytes = importer.ImportJsonAsset(baseField.TemplateField, sr, out exceptionMessage);
                            }
                            else
                            {
                                Console.WriteLine("JSON import requires proper deserialization. Trying text import instead...");
                                sr.BaseStream.Position = 0;
                                bytes = importer.ImportTextAsset(sr, out exceptionMessage);
                            }
                        }
                        else // txt
                        {
                            bytes = importer.ImportTextAsset(sr, out exceptionMessage);
                        }

                        if (bytes == null)
                        {
                            Console.WriteLine($"Error reading dump file: {exceptionMessage}");
                            return;
                        }
                    }
                }
                finally
                {
                    manager.UnloadAllAssetsFiles(true);
                }

                using (FileStream fs = File.OpenRead(fileToPatch))
                using (AssetsFileReader reader = new AssetsFileReader(fs))
                {
                    afile = new AssetsFile();
                    afile.Read(reader);
                    
                    asset = afile.GetAssetInfo(dumpFilePathId);
                    if (asset == null)
                    {
                        Console.WriteLine($"Asset with pathID {dumpFilePathId} not found after re-opening file");
                        return;
                    }

                    var replacer = new AssetsReplacerFromMemory(dumpFilePathId, asset.TypeId, (ushort)asset.TypeIdOrIndex, bytes);
                    List<AssetsReplacer> reps = new List<AssetsReplacer> { replacer };

                    string tempFile = outputFile;
                    bool overwriteOriginal = false;
                    
                    if (outputFile.ToLower() == "overwrite")
                    {
                        overwriteOriginal = true;
                        tempFile = fileToPatch + ".patch";
                    }

                    using (var writer = new AssetsFileWriter(tempFile))
                    {
                        afile.Write(writer, 0, reps, null);
                    }

                    if (overwriteOriginal)
                    {
                        fs.Close();
                        reader.Close();
                        
                        File.Delete(fileToPatch);
                        File.Move(tempFile, fileToPatch);
                        Console.WriteLine($"Patched asset (File ID: {fileId}, Path ID: {dumpFilePathId}) from {dumpType} dump into {fileToPatch}");
                    }
                    else
                    {
                        Console.WriteLine($"Patched asset (File ID: {fileId}, Path ID: {dumpFilePathId}) from {dumpType} dump into {outputFile}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return;
            }
        }

        // ---------------- GET ASSET INFO ----------------
        private static void GetAssetInfo(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: getassetinfo <assets file> <path id> [file id]");
                Console.WriteLine("  <assets file>: Input assets file (can be .assets or .unity3d)");
                Console.WriteLine("  <path id>: Path ID of the asset to get info about");
                Console.WriteLine("  [file id]: (optional) File ID if multiple files (default: 0)");
                return;
            }

            string assetsFile = args[1];
            if (!long.TryParse(args[2], out long pathId))
            {
                Console.WriteLine("Invalid path ID");
                return;
            }

            // پارامتر اختیاری File ID
            int fileId = 0;
            if (args.Length >= 4)
            {
                if (!int.TryParse(args[3], out fileId))
                {
                    Console.WriteLine("Invalid file ID");
                    return;
                }
            }

            if (!File.Exists(assetsFile))
            {
                Console.WriteLine($"File {assetsFile} does not exist!");
                return;
            }

            var manager = new AssetsManager();
            
            try
            {
                var inst = manager.LoadAssetsFile(assetsFile, false);
                var assetInfo = inst.file.GetAssetInfo(pathId);
                
                if (assetInfo == null)
                {
                    Console.WriteLine($"Asset with Path ID {pathId} not found in File ID {fileId}");
                    return;
                }
                
                Console.WriteLine($"Asset Information:");
                Console.WriteLine($"  File ID: {fileId}");
                Console.WriteLine($"  Path ID: {assetInfo.PathId}");
                Console.WriteLine($"  Type ID: 0x{assetInfo.TypeId:X8}");
                Console.WriteLine($"  Byte Size: {assetInfo.ByteSize} bytes");
                Console.WriteLine($"  Absolute Position: 0x{assetInfo.AbsoluteByteStart:X}");
                Console.WriteLine($"  Type ID or Index: {assetInfo.TypeIdOrIndex}");
                
                bool hasTypeTree = inst.file.Metadata.TypeTreeEnabled;
                Console.WriteLine($"  Type Tree Enabled: {hasTypeTree}");
                
                if (hasTypeTree)
                {
                    var templateField = manager.GetTemplateBaseField(inst, assetInfo);
                    if (templateField != null && !string.IsNullOrEmpty(templateField.Type))
                    {
                        Console.WriteLine($"  Type Name: {templateField.Type}");
                    }
                    else
                    {
                        Console.WriteLine($"  Type Name: {GetCommonTypeName(assetInfo.TypeId)}");
                    }
                }
                else
                {
                    Console.WriteLine($"  Type Name: {GetCommonTypeName(assetInfo.TypeId)}");
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

        // ---------------- LIST ASSETS ----------------
        private static void ListAssets(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: listassets <assets file> [output file]");
                Console.WriteLine("  <assets file>: Input assets file (can be .assets or .unity3d)");
                Console.WriteLine("  [output file]: (optional) Output text file name");
                Console.WriteLine("      If not specified: prints to console");
                return;
            }

            string assetsFile = args[1];
            string? outputFile = args.Length >= 3 ? args[2] : null;
            
            if (!File.Exists(assetsFile))
            {
                Console.WriteLine($"File {assetsFile} does not exist!");
                return;
            }

            var manager = new AssetsManager();
            
            try
            {
                // Load class data
                string classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
                if (File.Exists(classDataPath))
                {
                    manager.LoadClassPackage(classDataPath);
                }
                
                var inst = manager.LoadAssetsFile(assetsFile, false);
                var file = inst.file;
                
                // Get Unity version for class database
                string unityVersion = file.Metadata.UnityVersion;
                if (unityVersion == "0.0.0" && inst.parentBundle != null)
                {
                    unityVersion = inst.parentBundle.file.Header.EngineVersion;
                }
                
                manager.LoadClassDatabaseFromPackage(unityVersion);
                
                // Create output
                StringBuilder output = new StringBuilder();
                output.AppendLine($"Assets in {Path.GetFileName(assetsFile)}:");
                output.AppendLine($"Unity Version: {unityVersion}");
                output.AppendLine($"File ID: 0"); // File ID is 0 for single file
                output.AppendLine($"File Size: {new FileInfo(assetsFile).Length} bytes");
                output.AppendLine($"Asset Count: {file.AssetInfos.Count}");
                output.AppendLine($"Type Tree Enabled: {file.Metadata.TypeTreeEnabled}");
                output.AppendLine();
                
                // Header with File ID
                output.AppendLine("File ID | Asset Name                | Type Name              | Byte Size | PathID      | TypeID");
                output.AppendLine("--------|--------------------------|------------------------|-----------|-------------|-----------");
                
                foreach (var assetInfo in file.AssetInfos)
                {
                    string typeName = "Unknown";
                    string assetName = "Unknown";
                    
                    // Get type name
                    try
                    {
                        if (file.Metadata.TypeTreeEnabled)
                        {
                            // Try to get type name from template field
                            try
                            {
                                var templateField = manager.GetTemplateBaseField(inst, assetInfo);
                                if (templateField != null && !string.IsNullOrEmpty(templateField.Type))
                                {
                                    typeName = templateField.Type;
                                }
                                else
                                {
                                    typeName = GetCommonTypeName(assetInfo.TypeId);
                                }
                            }
                            catch
                            {
                                typeName = GetCommonTypeName(assetInfo.TypeId);
                            }
                        }
                        else
                        {
                            typeName = GetCommonTypeName(assetInfo.TypeId);
                        }
                    }
                    catch
                    {
                        typeName = GetCommonTypeName(assetInfo.TypeId);
                    }
                    
                    // Get asset name by trying to read the asset
                    try
                    {
                        // Try to get base field to extract name
                        var baseField = manager.GetBaseField(inst, assetInfo);
                        if (baseField != null)
                        {
                            assetName = ExtractAssetNameFromField(baseField, typeName);
                        }
                        else
                        {
                            assetName = typeName;
                        }
                    }
                    catch
                    {
                        // If deserialization fails, try raw reading
                        assetName = TryReadAssetNameFromBytes(inst, assetInfo, typeName);
                    }
                    
                    // Clean up asset name
                    if (string.IsNullOrEmpty(assetName) || assetName == "null" || assetName == "None")
                    {
                        assetName = typeName;
                    }
                    
                    // Truncate long names for better formatting
                    if (assetName.Length > 24) assetName = assetName.Substring(0, 21) + "...";
                    if (typeName.Length > 22) typeName = typeName.Substring(0, 19) + "...";
                    
                    // Format the line with File ID
                    output.AppendLine($"0       | {assetName,-24} | {typeName,-22} | {assetInfo.ByteSize,9} | {assetInfo.PathId,11} | 0x{assetInfo.TypeId:X8}");
                }
                
                // Display or save output
                if (outputFile != null)
                {
                    File.WriteAllText(outputFile, output.ToString());
                    Console.WriteLine($"Asset list saved to: {outputFile}");
                }
                else
                {
                    Console.WriteLine(output.ToString());
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
        
        private static string ExtractAssetNameFromField(AssetTypeValueField baseField, string typeName)
        {
            try
            {
                // Check common name fields in Unity assets
                string[] nameFields = { "m_Name", "name", "Name", "m_GameObject", "m_TextureName", "m_ShaderName" };
                
                foreach (var fieldName in nameFields)
                {
                    var field = baseField[fieldName];
                    if (field != null && field.Value != null && field.Value.ValueType == AssetValueType.String)
                    {
                        string name = field.Value.AsString;
                        if (!string.IsNullOrEmpty(name) && name != "null" && name != "None")
                        {
                            return name;
                        }
                    }
                }
                
                // Special handling for specific types
                if (typeName == "GameObject")
                {
                    var nameField = baseField["m_Name"];
                    if (nameField != null && nameField.Value != null && nameField.Value.ValueType == AssetValueType.String)
                    {
                        return nameField.Value.AsString;
                    }
                }
                else if (typeName == "MonoBehaviour")
                {
                    var scriptField = baseField["m_Script"];
                    if (scriptField != null)
                    {
                        var nameField = scriptField["m_Name"];
                        if (nameField != null && nameField.Value != null && nameField.Value.ValueType == AssetValueType.String)
                        {
                            return nameField.Value.AsString + " (Script)";
                        }
                    }
                }
                else if (typeName == "TextAsset")
                {
                    var nameField = baseField["m_Name"];
                    if (nameField != null && nameField.Value != null && nameField.Value.ValueType == AssetValueType.String)
                    {
                        return nameField.Value.AsString;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            
            return typeName;
        }
        
        private static string TryReadAssetNameFromBytes(AssetsFileInstance inst, AssetFileInfo assetInfo, string typeName)
        {
            try
            {
                var reader = inst.file.Reader;
                long originalPosition = reader.Position;
                
                try
                {
                    reader.Position = assetInfo.AbsoluteByteStart;
                    
                    // Skip type tree if enabled
                    if (inst.file.Metadata.TypeTreeEnabled)
                    {
                        // Skip type tree size
                        reader.Position += 4;
                        
                        // Try to find string in first 200 bytes
                        long startPos = reader.Position;
                        long endPos = Math.Min(startPos + 200, assetInfo.AbsoluteByteStart + assetInfo.ByteSize);
                        
                        for (long pos = startPos; pos < endPos - 4; pos++)
                        {
                            reader.Position = pos;
                            try
                            {
                                int length = reader.ReadInt32();
                                if (length > 0 && length < 100 && pos + 4 + length <= endPos)
                                {
                                    string possibleName = reader.ReadStringLength(length);
                                    if (!string.IsNullOrEmpty(possibleName) && 
                                        possibleName.All(c => char.IsLetterOrDigit(c) || c == '_' || c == ' ' || c == '-'))
                                    {
                                        return possibleName;
                                    }
                                }
                            }
                            catch
                            {
                                // Continue searching
                            }
                        }
                    }
                    else
                    {
                        // Type tree disabled, try direct reading
                        for (int offset = 0; offset < Math.Min(100, assetInfo.ByteSize); offset += 4)
                        {
                            reader.Position = assetInfo.AbsoluteByteStart + offset;
                            try
                            {
                                int length = reader.ReadInt32();
                                if (length > 0 && length < 100 && offset + 4 + length < assetInfo.ByteSize)
                                {
                                    string possibleName = reader.ReadStringLength(length);
                                    if (!string.IsNullOrEmpty(possibleName) && 
                                        possibleName.All(c => char.IsLetterOrDigit(c) || c == '_' || c == ' ' || c == '-'))
                                    {
                                        return possibleName;
                                    }
                                }
                            }
                            catch
                            {
                                // Continue searching
                            }
                        }
                    }
                }
                finally
                {
                    reader.Position = originalPosition;
                }
            }
            catch
            {
                // Ignore all errors
            }
            
            return typeName;
        }

        private static string GetCommonTypeName(int typeId)
        {
            // Common Unity asset types
            return typeId switch
            {
                0x01 => "GameObject",
                0x02 => "Component",
                0x03 => "LevelGameManager",
                0x04 => "Transform",
                0x05 => "TimeManager",
                0x06 => "GlobalGameManager",
                0x08 => "Behaviour",
                0x09 => "GameManager",
                0x0B => "AudioManager",
                0x0C => "ParticleAnimator",
                0x0D => "InputManager",
                0x0E => "EllipsoidParticleEmitter",
                0x0F => "Pipeline",
                0x11 => "EditorExtension",
                0x12 => "Physics2DSettings",
                0x13 => "Camera",
                0x14 => "Material",
                0x15 => "MeshRenderer",
                0x17 => "Renderer",
                0x18 => "ParticleRenderer",
                0x1B => "Texture",
                0x1C => "Texture2D",
                0x1D => "Scene",
                0x1E => "RenderTexture",
                0x1F => "Mesh",
                0x20 => "Shader",
                0x21 => "TextAsset",
                0x22 => "RGBA32",
                0x23 => "GUID",
                0x25 => "AssetBundle",
                0x28 => "MonoBehaviour",
                0x29 => "MonoScript",
                0x2B => "Font",
                0x2C => "PlayerSettings",
                0x2D => "NamedObject",
                0x2E => "GUITexture",
                0x2F => "GUIText",
                0x30 => "GUIElement",
                0x31 => "PhysicMaterial",
                0x32 => "SphereCollider",
                0x33 => "CapsuleCollider",
                0x34 => "BoxCollider",
                0x35 => "PolygonCollider",
                0x36 => "MeshCollider",
                0x38 => "WheelCollider",
                0x3A => "Rigidbody",
                0x40 => "AnimationClip",
                0x41 => "ConstantForce",
                0x43 => "WorldParticleCollider",
                0x44 => "TagManager",
                0x45 => "AudioListener",
                0x46 => "AudioSource",
                0x47 => "AudioClip",
                0x48 => "RenderTexture",
                0x49 => "MeshFilter",
                0x4A => "OcclusionPortal",
                0x4B => "Skybox",
                0x53 => "QualitySettings",
                0x54 => "ShaderVariantCollection",
                0x55 => "ResourceManager",
                0x56 => "NetworkManager",
                0x57 => "PreloadData",
                0x58 => "MovieTexture",
                0x59 => "Texture3D",
                0x5A => "Cubemap",
                0x5B => "Avatar",
                0x5C => "AnimatorController",
                0x5D => "RuntimeAnimatorController",
                0x5E => "ScriptMapper",
                0x5F => "Animator",
                0x60 => "TrailRenderer",
                0x61 => "DelayedCallManager",
                0x62 => "TextMesh",
                0x63 => "RenderSettings",
                0x64 => "Light",
                0x65 => "CGProgram",
                0x66 => "BaseAnimationTrack",
                0x67 => "Animation",
                0x68 => "MonoManager",
                0x69 => "Texture2DArray",
                0x6A => "CubemapArray",
                0x6B => "Joint",
                0x6C => "CircleCollider2D",
                0x6D => "HingeJoint",
                0x6E => "PolygonCollider2D",
                0x6F => "BoxCollider2D",
                0x70 => "PhysicsMaterial2D",
                0x71 => "MeshParticleEmitter",
                0x72 => "MeshRenderer",
                0x73 => "ParticleEmitter",
                0x74 => "BuildSettings",
                0x75 => "AssetBundleManifest",
                0x76 => "RuntimeInitializeOnLoadManager",
                0x78 => "UnityConnectSettings",
                0x79 => "AvatarMask",
                0x7A => "VideoClip",
                0x7B => "ParticleSystem",
                0x7C => "ParticleSystemRenderer",
                0x7D => "ShaderKeyword",
                0x7E => "CachedSpriteAtlas",
                0x7F => "ReflectionProbe",
                0x80 => "Terrain",
                0x81 => "LightProbeGroup",
                0x82 => "AnimatorOverrideController",
                0x83 => "CanvasRenderer",
                0x84 => "Canvas",
                0x85 => "RectTransform",
                0x86 => "CanvasGroup",
                0x87 => "BillboardAsset",
                0x88 => "BillboardRenderer",
                0x89 => "SpeedTreeWindAsset",
                0x8A => "AnchoredJoint2D",
                0x8B => "Joint2D",
                0x8C => "SpringJoint2D",
                0x8D => "DistanceJoint2D",
                0x8E => "HingeJoint2D",
                0x8F => "SliderJoint2D",
                0x90 => "WheelJoint2D",
                0x91 => "NavMeshProjectSettings",
                0x92 => "NavMeshData",
                0x93 => "AudioMixer",
                0x94 => "AudioMixerController",
                0x95 => "AudioMixerGroupController",
                0x96 => "AudioMixerEffectController",
                0x97 => "AudioMixerSnapshotController",
                0x98 => "PhysicsUpdateBehaviour2D",
                0x99 => "ConstantForce2D",
                0x9A => "Effector2D",
                0x9B => "AreaEffector2D",
                0x9C => "PointEffector2D",
                0x9D => "PlatformEffector2D",
                0x9E => "SurfaceEffector2D",
                0x9F => "BuoyancyEffector2D",
                0xA0 => "RelativeJoint2D",
                0xA1 => "FixedJoint2D",
                0xA2 => "FrictionJoint2D",
                0xA3 => "TargetJoint2D",
                0xA4 => "LightProbes",
                0xA5 => "LightProbeProxyVolume",
                0xA6 => "SampleClip",
                0xA7 => "AudioMixerSnapshot",
                0xA8 => "AudioMixerGroup",
                0xA9 => "AssetBundle",
                0xAA => "AssetBundleManifest",
                _ => $"0x{typeId:X8}"
            };
        }

        // ---------------- COMPRESS BUNDLE ----------------
        private static void CompressBundle(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: compressbundle <bundle file> <output> <lz4|lzma>");
                return;
            }

            string bundleFile = args[1];
            string outputFile = args[2];
            string compTypeStr = args[3].ToLower();
            
            AssetBundleCompressionType compType = compTypeStr switch
            {
                "lz4" => AssetBundleCompressionType.LZ4,
                "lzma" => AssetBundleCompressionType.LZMA,
                _ => AssetBundleCompressionType.None
            };

            if (compType == AssetBundleCompressionType.None)
            {
                Console.WriteLine("Invalid compression type. Use 'lz4' or 'lzma'");
                return;
            }

            if (!File.Exists(bundleFile))
            {
                Console.WriteLine($"File {bundleFile} does not exist!");
                return;
            }

            try
            {
                var manager = new AssetsManager();
                var bundleInst = manager.LoadBundleFile(bundleFile, false);
                
                using (var fs = File.Open(outputFile, FileMode.Create))
                using (var writer = new AssetsFileWriter(fs))
                {
                    bundleInst.file.Pack(bundleInst.file.Reader, writer, compType, true);
                }
                
                Console.WriteLine($"Bundle compressed to {outputFile} using {compTypeStr.ToUpper()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        // ---------------- DECOMPRESS BUNDLE ----------------
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
                var manager = new AssetsManager();
                var bundleInst = manager.LoadBundleFile(bundleFile, false);
                
                using (var fs = File.Open(outputFile, FileMode.Create))
                using (var writer = new AssetsFileWriter(fs))
                {
                    bundleInst.file.Unpack(writer);
                }
                
                Console.WriteLine($"Bundle decompressed to {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        // ---------------- SEARCH ASSET ----------------
        private static void SearchAsset(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: searchasset <assets file> <search term>");
                Console.WriteLine("  <assets file>: Input assets file (can be .assets or .unity3d)");
                Console.WriteLine("  <search term>: Text to search in asset names");
                return;
            }

            string assetsFile = args[1];
            string searchTerm = args[2];

            if (!File.Exists(assetsFile))
            {
                Console.WriteLine($"File {assetsFile} does not exist!");
                return;
            }

            var manager = new AssetsManager();
            
            try
            {
                // Load class data
                string classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
                if (File.Exists(classDataPath))
                {
                    manager.LoadClassPackage(classDataPath);
                }
                
                var inst = manager.LoadAssetsFile(assetsFile, false);
                var file = inst.file;
                
                // Get Unity version for class database
                string unityVersion = file.Metadata.UnityVersion;
                if (unityVersion == "0.0.0" && inst.parentBundle != null)
                {
                    unityVersion = inst.parentBundle.file.Header.EngineVersion;
                }
                
                manager.LoadClassDatabaseFromPackage(unityVersion);
                
                Console.WriteLine($"Searching for '{searchTerm}' in {Path.GetFileName(assetsFile)} (File ID: 0)...");
                Console.WriteLine();
                
                bool found = false;
                foreach (var assetInfo in file.AssetInfos)
                {
                    string typeName = "Unknown";
                    string assetName = "Unknown";
                    
                    // Get type name
                    try
                    {
                        if (file.Metadata.TypeTreeEnabled)
                        {
                            // Try to get type name from template field
                            try
                            {
                                var templateField = manager.GetTemplateBaseField(inst, assetInfo);
                                if (templateField != null && !string.IsNullOrEmpty(templateField.Type))
                                {
                                    typeName = templateField.Type;
                                }
                                else
                                {
                                    typeName = GetCommonTypeName(assetInfo.TypeId);
                                }
                            }
                            catch
                            {
                                typeName = GetCommonTypeName(assetInfo.TypeId);
                            }
                        }
                        else
                        {
                            typeName = GetCommonTypeName(assetInfo.TypeId);
                        }
                    }
                    catch
                    {
                        typeName = GetCommonTypeName(assetInfo.TypeId);
                    }
                    
                    // Get asset name
                    try
                    {
                        var baseField = manager.GetBaseField(inst, assetInfo);
                        if (baseField != null)
                        {
                            assetName = ExtractAssetNameFromField(baseField, typeName);
                        }
                        else
                        {
                            assetName = typeName;
                        }
                    }
                    catch
                    {
                        assetName = TryReadAssetNameFromBytes(inst, assetInfo, typeName);
                    }
                    
                    // Clean up asset name
                    if (string.IsNullOrEmpty(assetName) || assetName == "null" || assetName == "None")
                    {
                        assetName = typeName;
                    }
                    
                    // Search in both asset name and type name (case insensitive)
                    if (assetName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        typeName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Console.WriteLine($"Found: {assetName} | Type: {typeName} | File ID: 0 | PathID: {assetInfo.PathId} | Size: {assetInfo.ByteSize} bytes");
                        found = true;
                    }
                }
                
                if (!found)
                {
                    Console.WriteLine("No assets found matching the search term.");
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

        // ---------------- UNITY3D INFO ----------------
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

            try
            {
                // Try to load as assets file
                var manager = new AssetsManager();
                var inst = manager.LoadAssetsFile(unity3dFile, false);
                
                Console.WriteLine($"Unity3D File Information:");
                Console.WriteLine($"  File Name: {Path.GetFileName(unity3dFile)}");
                Console.WriteLine($"  File Size: {new FileInfo(unity3dFile).Length} bytes");
                Console.WriteLine($"  Unity Version: {inst.file.Metadata.UnityVersion}");
                Console.WriteLine($"  Asset Count: {inst.file.AssetInfos.Count}");
                Console.WriteLine($"  Type Tree Enabled: {inst.file.Metadata.TypeTreeEnabled}");
                
                manager.UnloadAllAssetsFiles(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading as Unity3D file: {ex.Message}");
                
                // Try to load as bundle
                try
                {
                    var manager = new AssetsManager();
                    var bundleInst = manager.LoadBundleFile(unity3dFile, false);
                    
                    Console.WriteLine($"Unity3D File Information (Bundle):");
                    Console.WriteLine($"  File Name: {Path.GetFileName(unity3dFile)}");
                    Console.WriteLine($"  File Size: {new FileInfo(unity3dFile).Length} bytes");
                    Console.WriteLine($"  Engine Version: {bundleInst.file.Header.EngineVersion}");
                    Console.WriteLine($"  Compression Type: {bundleInst.file.Header.GetCompressionType()}");
                    Console.WriteLine($"  Directory Count: {bundleInst.file.BlockAndDirInfo.DirectoryInfos.Length}");
                    
                    manager.UnloadAllBundleFiles();
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"Also failed to load as bundle: {ex2.Message}");
                    Console.WriteLine("This may not be a valid Unity3D file.");
                }
            }
        }

        // ---------------- EXISTING METHODS ----------------
        private static string GetMainFileName(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-"))
                    return args[i];
            }
            return string.Empty;
        }

        private static HashSet<string> GetFlags(string[] args)
        {
            HashSet<string> flags = new HashSet<string>();
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                    flags.Add(args[i]);
            }
            return flags;
        }

        private static AssetBundleFile DecompressBundle(string file, string? decompFile)
        {
            AssetBundleFile bun = new AssetBundleFile();

            Stream fs = File.OpenRead(file);
            AssetsFileReader r = new AssetsFileReader(fs);

            bun.Read(r);
            if (bun.Header.GetCompressionType() != 0)
            {
                Stream nfs;
                if (decompFile == null)
                    nfs = new MemoryStream();
                else
                    nfs = File.Open(decompFile, FileMode.Create, FileAccess.ReadWrite);

                AssetsFileWriter w = new AssetsFileWriter(nfs);
                bun.Unpack(w);

                nfs.Position = 0;
                fs.Close();

                fs = nfs;
                r = new AssetsFileReader(fs);

                bun = new AssetBundleFile();
                bun.Read(r);
            }

            return bun;
        }

        private static string GetNextBackup(string affectedFilePath)
        {
            for (int i = 0; i < 10000; i++)
            {
                string bakName = $"{affectedFilePath}.bak{i.ToString().PadLeft(4, '0')}";
                if (!File.Exists(bakName))
                {
                    return bakName;
                }
            }

            Console.WriteLine("Too many backups, exiting for your safety.");
            return null;
        }

        private static void BatchExportBundle(string[] args)
        {
            string exportDirectory = GetMainFileName(args);
            if (!Directory.Exists(exportDirectory))
            {
                Console.WriteLine("Directory does not exist!");
                return;
            }

            HashSet<string> flags = GetFlags(args);
            foreach (string file in Directory.EnumerateFiles(exportDirectory))
            {
                string decompFile = $"{file}.decomp";

                if (flags.Contains("-md"))
                    decompFile = null;

                if (!File.Exists(file))
                {
                    Console.WriteLine($"File {file} does not exist!");
                    continue;
                }

                DetectedFileType fileType = FileTypeDetector.DetectFileType(file);
                if (fileType != DetectedFileType.BundleFile)
                {
                    continue;
                }

                Console.WriteLine($"Decompressing {file}...");
                AssetBundleFile bun = DecompressBundle(file, decompFile);

                int entryCount = bun.BlockAndDirInfo.DirectoryInfos.Length;
                for (int i = 0; i < entryCount; i++)
                {
                    string name = bun.BlockAndDirInfo.DirectoryInfos[i].Name;
                    byte[] data = BundleHelper.LoadAssetDataFromBundle(bun, i);
                    string outName;
                    if (flags.Contains("-keepnames"))
                        outName = Path.Combine(exportDirectory, name);
                    else
                        outName = Path.Combine(exportDirectory, $"{Path.GetFileName(file)}_{name}");
                    Console.WriteLine($"Exporting {outName}...");
                    File.WriteAllBytes(outName, data);
                }

                bun.Close();

                if (!flags.Contains("-kd") && !flags.Contains("-md") && decompFile != null && File.Exists(decompFile))
                    File.Delete(decompFile);

                Console.WriteLine("Done.");
            }
        }

        private static void BatchImportBundle(string[] args)
        {
            string importDirectory = GetMainFileName(args);
            if (!Directory.Exists(importDirectory))
            {
                Console.WriteLine("Directory does not exist!");
                return;
            }

            HashSet<string> flags = GetFlags(args);
            foreach (string file in Directory.EnumerateFiles(importDirectory))
            {
                string decompFile = $"{file}.decomp";

                if (flags.Contains("-md"))
                    decompFile = null;

                if (!File.Exists(file))
                {
                    Console.WriteLine($"File {file} does not exist!");
                    continue;
                }

                DetectedFileType fileType = FileTypeDetector.DetectFileType(file);
                if (fileType != DetectedFileType.BundleFile)
                {
                    continue;
                }

                Console.WriteLine($"Decompressing {file} to {(decompFile ?? "memory")}...");
                AssetBundleFile bun = DecompressBundle(file, decompFile);

                List<BundleReplacer> reps = new List<BundleReplacer>();
                List<Stream> streams = new List<Stream>();

                int entryCount = bun.BlockAndDirInfo.DirectoryInfos.Length;
                for (int i = 0; i < entryCount; i++)
                {
                    string name = bun.BlockAndDirInfo.DirectoryInfos[i].Name;
                    string matchName = Path.Combine(importDirectory, $"{Path.GetFileName(file)}_{name}");

                    if (File.Exists(matchName))
                    {
                        FileStream fs = File.OpenRead(matchName);
                        long length = fs.Length;
                        reps.Add(new BundleReplacerFromStream(name, name, true, fs, 0, length));
                        streams.Add(fs);
                        Console.WriteLine($"Importing {matchName}...");
                    }
                }

                byte[] data;
                using (MemoryStream ms = new MemoryStream())
                using (AssetsFileWriter w = new AssetsFileWriter(ms))
                {
                    bun.Write(w, reps);
                    data = ms.ToArray();
                }
                Console.WriteLine($"Writing changes to {file}...");

                foreach (Stream stream in streams)
                    stream.Close();

                bun.Close();

                File.WriteAllBytes(file, data);

                if (!flags.Contains("-kd") && !flags.Contains("-md") && decompFile != null && File.Exists(decompFile))
                    File.Delete(decompFile);

                Console.WriteLine("Done.");
            }
        }

        private static void ApplyEmip(string[] args)
        {
            HashSet<string> flags = GetFlags(args);
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: applyemip <emip file> <directory>");
                return;
            }
            string emipFile = args[1];
            string rootDir = args[2];

            if (!File.Exists(emipFile))
            {
                Console.WriteLine($"File {emipFile} does not exist!");
                return;
            }

            InstallerPackageFile instPkg = new InstallerPackageFile();
            FileStream fs = File.OpenRead(emipFile);
            AssetsFileReader r = new AssetsFileReader(fs);
            instPkg.Read(r, true);

            Console.WriteLine($"Installing emip...");
            Console.WriteLine($"{instPkg.modName} by {instPkg.modCreators}");
            Console.WriteLine(instPkg.modDescription);

            foreach (var affectedFile in instPkg.affectedFiles)
            {
                string affectedFileName = Path.GetFileName(affectedFile.path);
                string affectedFilePath = Path.Combine(rootDir, affectedFile.path);

                if (affectedFile.isBundle)
                {
                    string decompFile = $"{affectedFilePath}.decomp";
                    string modFile = $"{affectedFilePath}.mod";
                    string bakFile = GetNextBackup(affectedFilePath);

                    if (bakFile == null)
                    {
                        return;
                    }

                    if (flags.Contains("-md"))
                        decompFile = null;

                    Console.WriteLine($"Decompressing {affectedFileName} to {decompFile ?? "memory"}...");
                    AssetBundleFile bun = DecompressBundle(affectedFilePath, decompFile);
                    List<BundleReplacer> reps = new List<BundleReplacer>();

                    foreach (var rep in affectedFile.replacers)
                    {
                        var bunRep = (BundleReplacer)rep;
                        if (bunRep is BundleReplacerFromAssets)
                        {
                            string assetName = bunRep.GetOriginalEntryName();
                            var bunRepInf = BundleHelper.GetDirInfo(bun, assetName);
                            long pos = bunRepInf.Offset;
                            bunRep.Init(bun.DataReader, pos, bunRepInf.DecompressedSize);
                        }
                        reps.Add(bunRep);
                    }

                    Console.WriteLine($"Writing {modFile}...");
                    using (FileStream mfs = File.Open(modFile, FileMode.Create))
                    using (AssetsFileWriter mw = new AssetsFileWriter(mfs))
                    {
                        bun.Write(mw, reps, instPkg.addedTypes);
                    }

                    bun.Close();

                    Console.WriteLine($"Swapping mod file...");
                    File.Move(affectedFilePath, bakFile);
                    File.Move(modFile, affectedFilePath);

                    if (!flags.Contains("-kd") && !flags.Contains("-md") && decompFile != null && File.Exists(decompFile))
                        File.Delete(decompFile);

                    Console.WriteLine($"Done.");
                }
                else // isAssetsFile
                {
                    string modFile = $"{affectedFilePath}.mod";
                    string bakFile = GetNextBackup(affectedFilePath);

                    if (bakFile == null)
                    {
                        return;
                    }

                    using (FileStream afs = File.OpenRead(affectedFilePath))
                    using (AssetsFileReader ar = new AssetsFileReader(afs))
                    {
                        AssetsFile assets = new AssetsFile();
                        assets.Read(ar);
                        List<AssetsReplacer> reps = new List<AssetsReplacer>();

                        foreach (var rep in affectedFile.replacers)
                        {
                            var assetsReplacer = (AssetsReplacer)rep;
                            reps.Add(assetsReplacer);
                        }

                        Console.WriteLine($"Writing {modFile}...");
                        using (FileStream mfs = File.Open(modFile, FileMode.Create))
                        using (AssetsFileWriter mw = new AssetsFileWriter(mfs))
                        {
                            assets.Write(mw, 0, reps, instPkg.addedTypes);
                        }
                    }

                    Console.WriteLine($"Swapping mod file...");
                    File.Move(affectedFilePath, bakFile);
                    File.Move(modFile, affectedFilePath);

                    Console.WriteLine($"Done.");
                }
            }
        }

        // ---------------- MAIN ENTRY ----------------
        public static void CLHMain(string[] args)
        {
            if (args.Length < 2)
            {
                PrintHelp();
                return;
            }

            string command = args[0];

            try
            {
                if (command == "batchexportbundle")
                {
                    BatchExportBundle(args);
                }
                else if (command == "batchimportbundle")
                {
                    BatchImportBundle(args);
                }
                else if (command == "applyemip")
                {
                    ApplyEmip(args);
                }
                else if (command == "patchrawasset")
                {
                    PatchRawAsset(args);
                }
                else if (command == "patchdumpasset")
                {
                    PatchDumpAsset(args);
                }
                else if (command == "getassetinfo")
                {
                    GetAssetInfo(args);
                }
                else if (command == "listassets")
                {
                    ListAssets(args);
                }
                else if (command == "compressbundle")
                {
                    CompressBundle(args);
                }
                else if (command == "decompressbundle")
                {
                    DecompressBundle(args);
                }
                else if (command == "searchasset")
                {
                    SearchAsset(args);
                }
                else if (command == "unity3dinfo")
                {
                    Unity3dInfo(args);
                }
                else
                {
                    Console.WriteLine($"Unknown command: {command}");
                    PrintHelp();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
