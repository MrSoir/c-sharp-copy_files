using System;
using System.IO;
using System.Collections.Generic;

namespace COPY_FILES
{
    class CopyFiles : WORKER.Worker
    {
        public CopyFiles(string[] sourcePaths, 
                         string targetPath)
                : base()
        {
            foreach(var sp in sourcePaths){
                base.workers.Add(
                    WORKER.WorkerDirEntry.createWorkerDirFromPath(sp, 
                                                                  tarBaseDir: targetPath, 
                                                                  processFunc:STATIC_FUNCTIONS.StaticFunctions.copyEntry) );
            }
            this.Name = "CopyFiles";
        }
    }
}
