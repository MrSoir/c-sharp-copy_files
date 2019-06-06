using System;
using System.IO;
using System.Collections.Generic;
using PATH_FUNCTIONS;
using WORKER;
using STATIC_FUNCTIONS;

namespace DUPLICATE_FILES
{
    class DuplicateFiles : WORKER.Worker
    {
        public DuplicateFiles(string[] sourcePaths)
                : base()
        {
            foreach(var sourcePath in sourcePaths){
                (string baseDir, string uniqueEntryName) = PathFunctions.generateUniquePath(sourcePath);
                var absTarPath = Path.Join(baseDir, uniqueEntryName);
                if(absTarPath != sourcePath){
                    base.workers.Add( WorkerDirEntry.createWorkerDirFromPath(baseDir: sourcePath,
                                                                             tarBaseDir: baseDir,
                                                                             tarEntryName: uniqueEntryName,
                                                                             processFunc: (acp, atp)=>{
                                                                                return StaticFunctions.copyEntry(acp, atp);
                                                                             })
                    );                                           
                }
            }
            this.Name = "DuplicateFiles";
        }
    }
}
