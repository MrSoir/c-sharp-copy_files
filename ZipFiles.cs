using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using STATIC_FUNCTIONS;
using PATH_FUNCTIONS;
using WORKER;
using System.Linq;

namespace ZIP_FILES
{
    class ZipFiles : WORKER.Worker
    {
        private string absTargetZipFilePath;
        // private ZipArchive zipArchive;
        // private FileStream zipFileStream;
        private delegate bool ZipArchiveOperatorDel(ZipArchive zipArchive);
        private HashSet<string> registeredRelEntryRootPaths = new HashSet<string>();

        public ZipFiles(string[] sourcePaths, 
                        string absTargetZipFilePath)
                : base()
        {
            absTargetZipFilePath = this.askForValidZipFilePath(absTargetZipFilePath);

            if(string.IsNullOrEmpty(absTargetZipFilePath))
            {
                this.Cancel();
                return;
            }
            this.absTargetZipFilePath = absTargetZipFilePath;

            var tarZipFileBaseDir = StaticFunctions.getBaseDirectory(absTargetZipFilePath);
            var tarZipFileName = StaticFunctions.getEntryName(absTargetZipFilePath);
            
            var zipCreatorWorker = WorkerDirEntry.createWorkerDirFromPath(absTargetZipFilePath,
                                                                          tarBaseDir: tarZipFileBaseDir,
                                                                          tarEntryName: tarZipFileName,
                                                                          processFunc: (string absSrcPath, string absTarPath)=>{
                                                                              return this.createArchive(absTarPath);
                                                                          },
                                                                          recursive: false);
            zipCreatorWorker.SrcMustExist = false;
            this.workers.Add( zipCreatorWorker );

            foreach(var sp in sourcePaths)
            {
                this.createWorkerFromSrcPath(sp);
            }

            this.TidyUpBeforeFinish += this.closeZipArchive;

            this.Name = "ZipFiles";
        }
        private string askForValidZipFilePath(string absTargetZipFilePath)
        {
            return StaticFunctions.EntryExists( absTargetZipFilePath) ?
                   PathFunctions.generateUniquePathJoined(absTargetZipFilePath) :
                   absTargetZipFilePath;
        }

        private bool operateOnArchive(ZipArchiveOperatorDel f, FileMode fm, ZipArchiveMode zam)
        {
            try{
                using(var zipFileStream = new FileStream(this.absTargetZipFilePath, fm))
                {
                    using(var zipArchive = new ZipArchive(zipFileStream, zam))
                    {
                        return f.Invoke(zipArchive);
                    }
                }
            }catch(Exception e){
                Console.WriteLine("--- " + e);
                return false;
            }
        }
        private bool createArchive(string tarZipFilePath)
        {
            return operateOnArchive((ZipArchive za)=>{return true;}, FileMode.CreateNew, ZipArchiveMode.Create);
        }
        private ZipArchiveEntry[] getZipArchiveEntries()
        {
            try{
                ZipArchiveEntry[] entries = null;

                using(var fs = new FileStream(this.absTargetZipFilePath, FileMode.Open))
                {
                    using(var zs = new ZipArchive(fs, ZipArchiveMode.Read))
                    {
                        entries = zs.Entries.ToArray();
                    }
                }
                return entries;
            }catch(Exception){
                return null;
            }
        }

        private bool checkIfEntryAlreadyExists(string relZipFilePath)
        {
            var entries = getZipArchiveEntries();
            if( entries == null ){
                return false;
            }
            foreach (ZipArchiveEntry entry in entries)
            {
                if(entry.FullName == relZipFilePath){
                    return true;
                }
            }
            return false;
        }
        private string getRelParentDir(string absBaseDir, string absSrcPath)
        {
           var absParentDir = StaticFunctions.getBaseDirectory(absSrcPath);
           return Path.GetRelativePath(absBaseDir, absParentDir);
        }
        private string generateUniqueRelZipPath(string absBaseDir, string absSrcPath)
        {
            var entryNameWExt = PathFunctions.getFileNameWithoutExtension(absSrcPath);
            var ext = PathFunctions.getExtension(absSrcPath);

            var parentDir = StaticFunctions.getBaseDirectory(absSrcPath);
            var relBaseDirPath = Path.GetRelativePath(absBaseDir, parentDir);

            return generateUniquePath_hlpr(relBaseDirPath, entryNameWExt, ext, this.checkIfEntryAlreadyExists);
        }
        private string generateUniqueZipBasePath(string absSrcPath)
        {
            var entryNameWExt = PathFunctions.getFileNameWithoutExtension(absSrcPath);
            var ext = PathFunctions.getExtension(absSrcPath);

            return generateUniquePath_hlpr("", entryNameWExt, ext, this.checkIfEntryRelRootPathAlreadyRegistered);
        }
        private delegate bool PathExistsChecker(string path);
        private string generateUniquePath_hlpr(string baseDir, string entryNameWExt, string ext, PathExistsChecker validator)
        {
            var entryNameWExt_mod = entryNameWExt;
            var entryName = entryNameWExt_mod + ext;
            var tarPath = string.IsNullOrEmpty(baseDir) ? entryName : Path.GetRelativePath(baseDir, entryName);
            var cntr = 2;

            while( validator(tarPath) )
            {
                entryNameWExt_mod = entryNameWExt + "_(" + cntr++ + ")";
                entryName = entryNameWExt_mod + ext;
                tarPath = string.IsNullOrEmpty(baseDir) ? entryName : Path.GetRelativePath(baseDir, entryName);
            }
            return tarPath;
        }
        private bool checkIfEntryRelRootPathAlreadyRegistered(string entryRelRootPath)
        {
            return this.registeredRelEntryRootPaths.Contains(entryRelRootPath);
        }
        private delegate bool ZipToArchiveDel(string absSrcPath, string absBasPath);
        private void createWorkerFromSrcPath(string sourcePath){
            // hier muss noch getestet werden, ob der relZipPath von sourcePath bereits existiert -> dann alternativen Path generieren!
            
            string absBaseDir = StaticFunctions.getBaseDirectory(sourcePath);
            string tarEntryName = StaticFunctions.getEntryName(sourcePath);

            if(checkIfEntryRelRootPathAlreadyRegistered(tarEntryName))
            {
                var uniqueEntryName = generateUniqueZipBasePath(sourcePath);
                this.registeredRelEntryRootPaths.Add(uniqueEntryName);
                tarEntryName = uniqueEntryName;
            }
            this.registeredRelEntryRootPaths.Add(tarEntryName);

            ZipToArchiveDel zipdel = (string absSrcPath, string relZipFilePath)=>{
                return this.addEntryToZipArchive(absSrcPath, relZipFilePath);
            };
            var worker = WorkerDirEntry.createWorkerDirFromPath(baseDir: sourcePath,
                                                                tarBaseDir: "",
                                                                tarEntryName: tarEntryName,
                                                                processFunc: (string absSrcPath, string absTarPath)=>{
                                                                    return zipdel(absSrcPath, absTarPath);
                                                                });
            worker.TarMustNotExist = false;
            this.workers.Add( worker );
        }
        private void closeZipArchive(){
            // try{
            //     this.zipArchive?.Dispose();
            //     this.zipFileStream?.Dispose();
            // }catch(Exception e){
            //     Console.WriteLine(e);
            // }
        }
        private bool addEntryToZipArchive(string absSourcePath, string relArchivePath)
        {
            
            ZipArchiveOperatorDel entryCreator = null;
            if(Directory.Exists(absSourcePath))
            {
                entryCreator = (za) => {
                    try{
                        // ms-docs: Zip-Library uses Unix-Path separators, not system-dependent path-separators!! (to achieve platform-independence)
                        var dirRelArchivePath = PathFunctions.appenUnixDirSeparatorToPath(relArchivePath);
                        za.CreateEntry(dirRelArchivePath);
                        return true;
                    }catch(Exception e)
                    {
                        Console.WriteLine(e.Message);
                        return false;
                    }
                };
            }else if(File.Exists(absSourcePath)){
                entryCreator = (za) => {
                    try{
                        za.CreateEntryFromFile(absSourcePath, relArchivePath);
                        return true;
                    }catch(Exception e)
                    {
                        Console.WriteLine(e.Message);
                        return false;
                    }
                };
            }else{
                Console.WriteLine(string.Format("addEntryToZipArchive -  absSourcePath: '{0}' is neither file nor directory!", absSourcePath));
                return false;
            }
            
            try{
                return operateOnArchive(entryCreator, FileMode.Open, ZipArchiveMode.Update);
            }catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }
    }
}