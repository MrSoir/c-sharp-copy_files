using System;
using System.IO;
using System.Collections.Generic;
using STATIC_FUNCTIONS;
using WORKER;

namespace DELETE_FILES
{
    class DeleteFiles : WORKER.Worker
    {
        public DeleteFiles(string[] sourcePaths)
                : base()
        {
            foreach(var sp in sourcePaths){
                var worker = WORKER.WorkerDirEntry.createWorkerDirFromPath(
                    baseDir: sp, 
                    processFunc: (absSrcPath, AbsTarPath)=>{
                        return StaticFunctions.deleteEntry(absSrcPath);
                    }
                );
                worker.BottomUp = true;
                worker.TarMustNotExist = false;
                base.workers.Add( worker );
            }
            this.Name = "DeleteFiles";
        }
    }
}
