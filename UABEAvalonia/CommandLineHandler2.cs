﻿﻿using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;

namespace UABEAvalonia
{
    public class CommandLineHandler
    {
        public static void PrintHelp()
        {
            Console.WriteLine("UABE AVALONIA - Patch Dump Asset Only");
            Console.WriteLine("Usage: UABEAvalonia patchdumpasset <assets file> <dump file> [file id] [type] [output file]");
            Console.WriteLine("  <assets file>: Input assets file (.assets/.unity3d)");
            Console.WriteLine("  <dump file>: Dump file (.txt/.json)");
            Console.WriteLine("  [file id]: (optional) File ID (default: 0)");
            Console.WriteLine("  [type]: (optional) 'txt', 'json' or 'auto' (default: auto)");
            Console.WriteLine("  [output file]: (optional) Output file (default: <assets>.patch)");
        }

        private static void PatchDumpAsset(string[] args)
        {
            if (args.Length < 3)
            {
                PrintHelp();
                return;
            }

            string fileToPatch = args[1];
            string dumpFile = args[2];
            
            int fileId = 0;
            string dumpType = "auto";
            string outputFile = fileToPatch + ".patch";
            
            int argIndex = 3;
            
            if (argIndex < args.Length && int.TryParse(args[argIndex], out fileId))
                argIndex++;
            
            if (argIndex < args.Length)
            {
                string possibleType = args[argIndex].ToLower();
                if (possibleType == "txt" || possibleType == "json" || possibleType == "auto")
                {
                    dumpType = possibleType;
                    argIndex++;
                }
            }
            
            if (argIndex < args.Length)
                outputFile = args[argIndex];

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
                            dumpType = dumpFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "json" : "txt";

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
                                Console.WriteLine("Warning: Could not deserialize asset for JSON import");
                            }
                            
                            if (baseField != null)
                                bytes = importer.ImportJsonAsset(baseField.TemplateField, sr, out exceptionMessage);
                            else
                            {
                                Console.WriteLine("Trying text import instead...");
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
                        afile.Write(writer, 0, reps, null);

                    if (overwriteOriginal)
                    {
                        fs.Close();
                        reader.Close();
                        
                        File.Delete(fileToPatch);
                        File.Move(tempFile, fileToPatch);
                        Console.WriteLine($"Patched asset (Path ID: {dumpFilePathId}) into {fileToPatch}");
                    }
                    else
                    {
                        Console.WriteLine($"Patched asset (Path ID: {dumpFilePathId}) into {outputFile}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        public static void CLHMain(string[] args)
        {
            if (args.Length < 2)
            {
                PrintHelp();
                return;
            }

            string command = args[0];

            if (command == "patchdumpasset")
            {
                PatchDumpAsset(args);
            }
            else
            {
                Console.WriteLine($"This version only supports 'patchdumpasset' command");
                PrintHelp();
            }
        }
    }
}