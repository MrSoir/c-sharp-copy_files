using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.IO.Compression;
using STATIC_FUNCTIONS;
using PATH_FUNCTIONS;
using WORKER;

namespace UNZIP_FILE
{
    class UnzipFile : WORKER.Worker
    {
        private string archiveToExtract;
        private string extractionDir;
        private struct ZipEntryInfo{
            public long fileSize;
            public string zipEntryPath;
            public bool isDir;
            public ZipEntryInfo(string zipEntryPath,
                      long fileSize,
                      bool isDir)
            {
                this.zipEntryPath = zipEntryPath;
                this.fileSize = fileSize;
                this.isDir = isDir;
            }
        };
        private Dictionary<string, ZipEntryInfo> zipEntryInfos = new Dictionary<string, ZipEntryInfo>();
        public UnzipFile(string archiveToExtract, 
                         string extractionDir)
                : base()
        {
            this.archiveToExtract = archiveToExtract;

            if( Directory.Exists(extractionDir) )
            {
                // for testing:
                extractionDir = PathFunctions.generateUniquePathJoined(extractionDir);

                // actual code to execute (relies on GUI-components):

                // var user_answer = StaticFunctions.askForValidDirectoryPath(extractionDir);
                // if(user_answer.Item1 == StaticFunctions.USER_ANSWER.OK)
                // {
                //     extractionDir = user_answer.Item2;
                //     // check if something went wrong and user selected path does already exist:
                //     if( Directory.Exists(extractionDir) )
                //     {
                //         this.Cancel();
                //     }
                //     // now create user selected targetPath:
                //     if(!StaticFunctions.createDirectoryIfNotExistent(extractionDir))
                //     {
                //         this.Cancel();
                //     }
                // }else{
                //     this.Cancel();
                // }
            }
            // create target directory: extractionDir
            StaticFunctions.createDirectoryIfNotExistent(extractionDir);
            if( !Directory.Exists(extractionDir) )
            {
                this.Cancel();
            }

            this.extractionDir = extractionDir;

            if( !this.Cancelled )
            {
                var successfullyCreatedWorkers = createWorkers();
                if(!successfullyCreatedWorkers)
                {
                    this.Cancel();
                }
            }
            var cntr = 0;
            foreach(var w in this.workers)
            {
                Console.WriteLine(string.Format("worker[{0}]: {1}", cntr++, w.AbsSrcPath()));
                w.print();
            }

            this.Name = "UnzipFile";
        }
        private bool createWorkers()
        {
            var zipEntries = StaticFunctions.getEntriesFromArchive(this.archiveToExtract);
            if(zipEntries == null)
            {
                return false;
            }else{
                foreach(var entry in zipEntries)
                {
                    createWorker(entry);
                }
                return true;
            }
        }

        private WorkerDirEntry createDirWorkerIfNotAlreadyExistent(string zipDirPath, int cntr=0)
        {
            WorkerDirEntry worker;

            var entryName = StaticFunctions.getEntryName(zipDirPath);

            var zipBasePath = StaticFunctions.getBaseDirectory(zipDirPath);

            if( string.IsNullOrEmpty(zipBasePath) )
            {
                worker = WorkerDirEntry.createWorkerDirFromPath(zipDirPath,
                                                                tarBaseDir: this.extractionDir,
                                                                tarEntryName: entryName);
                this.workers.Add(worker);
            }else{
                var parentWorker = createDirWorkerIfNotAlreadyExistent(zipBasePath);
                if(parentWorker == null)
                {
                    Console.WriteLine(string.Format("createDirWorkerIfNotAlreadyExistent - parent worker should exist but does not! - zipDirPath: {0}", zipDirPath));
                    this.Cancel();
                    return null;
                }
                worker = WorkerDirEntry.createWorkerDirFromPath(baseDir: "",
                                                                entryName: entryName,
                                                                tarEntryName: entryName,
                                                                parentWorkerDirEntry: parentWorker);
                parentWorker.sub_workerDirEntries.Add(worker);                
            }
            worker.ProcessFunc = (absSrcPath, absTarPath)=>{
                return StaticFunctions.createDirectoryIfNotExistent(absTarPath);
            };
            worker.SrcMustExist = false;
            return worker;
        }
        private void createWorker(ZipArchiveEntry entry)
        {
            var entryPath = entry.FullName;
            var entryPathTrmd = PathFunctions.removePotUnixDirSepAtEnd(entryPath);

            var parentWorker = getParentWorker(entryPathTrmd);

            WorkerDirEntry worker = null;

            bool entryIsDir = zipEntryIsDir(entry);

            var zipBasePath = StaticFunctions.getBaseDirectory(entryPathTrmd);

            // bei zip-files kanns gern mal vorkommen, dass files nicht in sortierter reihenfolge geliefert werden bzw. dass
            // lediglich files vorhanden sind - dann kanns fuer ein file in einem unterordner gar kein parent-folder-Worker geben
            // - den zuerstemal anlegen => wichtig, falls ziel-parent-folder auf filesystem bereits existiert und user 
            // im extraktions-prozess einen alternativen ordner auswaehlt - dann muessen alle child-worker in abhaengigkeit stehen!
            if(parentWorker == null)
            {
                if( !string.IsNullOrEmpty(zipBasePath) )
                {
                    parentWorker = createDirWorkerIfNotAlreadyExistent(zipBasePath);
                }
            }

            if( parentWorker != null )
            {
                var entryName = StaticFunctions.getEntryName(entryPathTrmd);
                worker = new WorkerDirEntry(entryName, 
                                            baseDir: null, 
                                            parentWorkerDirEntry: parentWorker, 
                                            tarBaseDir: null, 
                                            tarEntryName: entryName);
                if(entryIsDir)
                {
                    parentWorker.sub_workerDirEntries.Add(worker);
                }else{
                    parentWorker.sub_workerFileEntries.Add(worker);
                }
            }else{
                var absTarPath = Path.Join(this.extractionDir, entryPath);
                worker = WorkerDirEntry.createWorkerDirFromPath(baseDir: "",
                                                                entryName: entryPath,
                                                                tarBaseDir: this.extractionDir,
                                                                tarEntryName: entryPath,
                                                                recursive: false);
                this.workers.Add( worker );
            }
            worker.SrcMustExist = false;
            worker.ProcessFunc = (absSrcPath, absTarPath)=>{
                return this.extractEntryToPath(entryPath, absTarPath);
            };

            this.zipEntryInfos.Add(entryPath, new ZipEntryInfo(entryPath, entry.Length, entryIsDir));
        }
        private WorkerDirEntry getParentWorker(string absZipEntryPath)
        {
            var zipBaseDir = StaticFunctions.getBaseDirectory(absZipEntryPath);
            if(string.IsNullOrEmpty(zipBaseDir))
            {
                return null;
            }
            foreach(var worker in this.workers)
            {
                var pw = getParentWorker_hlpr(zipBaseDir, worker);
                if(pw != null)
                {
                    return pw;
                }

            }
            return null;
        }
        private WorkerDirEntry getParentWorker_hlpr(string zipEntryBaseDir, WorkerDirEntry worker)
        {
            var workerAbsPath = worker.AbsSrcPath();
            workerAbsPath = PathFunctions.removePotUnixDirSepAtEnd(workerAbsPath);
            if(workerAbsPath == zipEntryBaseDir)
            {
                return worker;
            }
            foreach(var subw in worker.sub_workerDirEntries)
            {
                var pw = getParentWorker_hlpr(zipEntryBaseDir, subw);
                if(pw != null)
                {
                    return pw;
                }
            }
            return null;
        }        
        private bool extractEntryToPath(string zipEntryPath, string absTarPath)
        {
            if( !this.zipEntryInfos.ContainsKey(zipEntryPath) )
            {
                return false;
            }

            var zipEntryInfo = this.zipEntryInfos[zipEntryPath];

            if(zipEntryInfo.isDir)
            {
                var absTarDirPath = PathFunctions.removePotUnixDirSepAtEnd(absTarPath);
                return StaticFunctions.createDirectoryIfNotExistent(absTarDirPath);
            }else{
                try{
                    return extractZipEntryToFile(zipEntryPath, absTarPath);
                }catch(Exception e){
                    Console.WriteLine(e);
                    return false;
                }
            }
        }
        private bool extractZipEntryToFile(string zipEntryPath, string absTarPath)
        {
            try{
                if(File.Exists(absTarPath))
                {
                    return false;
                }
                using(var ifs = new FileStream(this.archiveToExtract, FileMode.Open))
                {
                    using(var za = new ZipArchive(ifs, ZipArchiveMode.Read))
                    {
                        var zae = za.GetEntry(zipEntryPath);
                        zae.ExtractToFile(absTarPath);
                        return File.Exists(absTarPath);
                    }
                }
            }catch(Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        private bool zipEntryIsDir(ZipArchiveEntry entry)
        {
            var entryPath = entry.FullName;
            return entryPath[entryPath.Length-1] == '/' && entry.Length == 0;
        }
        private bool isZipSubEntry(ZipArchiveEntry potParentEntry, ZipArchiveEntry potSubEntry)
        {
            return isZipSubEntry(potParentEntry.FullName, potSubEntry.FullName);
        }
        private bool isZipSubEntry(string potParentEntry, string potSubEntry)
        {
            potParentEntry = PathFunctions.removePotUnixDirSepAtEnd(potParentEntry);
            potSubEntry = PathFunctions.removePotUnixDirSepAtEnd(potSubEntry);

            var subBaseDir = StaticFunctions.getBaseDirectory(potSubEntry);

            return potParentEntry == subBaseDir;
        }
    }
}
