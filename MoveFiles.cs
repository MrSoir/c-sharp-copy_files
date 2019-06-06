using System;
using System.IO;
using System.Collections.Generic;
using PATH_FUNCTIONS;
using WORKER;
using STATIC_FUNCTIONS;

namespace MOVE_FILES
{
    class MoveFiles : WORKER.Worker
    {
        private string[] sourcePaths;
        private string targetPath;
        public MoveFiles(string[] sourcePaths,
                         string targetPath)
                : base()
        {
            this.sourcePaths = sourcePaths;
            this.targetPath = targetPath;

            foreach(var sourcePath in sourcePaths){
                this.workers.Add( WorkerDirEntry.createWorkerDirFromPath(sourcePath, tarBaseDir: targetPath, 
                                                                         processFunc: StaticFunctions.moveEntry) );
            }
            this.setOnFinished((bool totalSuccess) =>{
                this.callAfterMoving(totalSuccess);
            });
            this.Name = "MoveFiles";
        }

        private void callAfterMoving(bool totalSuccess)
        {
            if(this.Cancelled){
                this.finish();
            }else{
                this.PostMessage?.Report("processing");

                List<WorkerDirEntry> failedMovementWorkers = new List<WorkerDirEntry>();
                foreach(var fw in this.finishedWorkers)
                {
                    var succeeded = fw.EvalSuccess();
                    if( !succeeded )
                    {
                        fw.evalSubEntries();
                        fw.ProcessFunc = StaticFunctions.copyEntry;
                        fw.reset();
                        failedMovementWorkers.Add( fw );
                    }
                }
                if(failedMovementWorkers.Count > 0)
                {
                    this.setWorkers(failedMovementWorkers);
                    this.setOnFinished((bool totSuccess)=>{
                        this.finish();
                    });
                    this.execute();
                }else{
                    this.finish();
                }
            }
        }
        private void callAfterCopyBackupWorker(bool succeeed)
        {
            this.resetOnFinishedCaller();

            if(this.Cancelled){
                this.finish();
            }else{
                List<WorkerDirEntry> workers_failedToCopy = new List<WorkerDirEntry>();
                List<WorkerDirEntry> workers_failedToDeleteSrc = new List<WorkerDirEntry>();
                foreach(var worker in this.workers)
                {
                    var worker_totalSuccess = worker.EvalSuccess();
                    if(worker_totalSuccess)
                    {
                        var success = StaticFunctions.deleteEntry( worker.AbsSrcPath() );
                        if( !success )
                        {
                            workers_failedToDeleteSrc.Add( worker );
                        }
                    }else{
                        workers_failedToCopy.Add( worker );
                    }
                }
                this.finish();
            }
        }
    }
}
