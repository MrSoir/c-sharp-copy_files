using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using STATIC_FUNCTIONS;

namespace WORKER
{
    class Worker
    {
        protected string Name {get;set;} = "Worker";
        private bool _cancelled = false;
        public bool Cancelled {get{return _cancelled;}}
        private long entriesToProcess = 0;
        private long entriesAlrProcessed = 0;
        protected List<WorkerDirEntry> workers = new List<WorkerDirEntry>();
        protected List<WorkerDirEntry> finishedWorkers = new List<WorkerDirEntry>();
        private WorkerDirEntry crntly_prcsd_wrkr;
        public bool TotalSuccess {get{
            foreach(var w in this.workers)
            {
                if( !w.EvalSuccess() )
                {
                    return false;
                }
            }
            return true;
        }}
        public delegate void OnFinishedDel(bool totalSuccess);
        public delegate void OnCancelledDel();
        public event OnFinishedDel OnFinished;
        public event OnCancelledDel OnCancelled;
        // public delegate void PostMessageDel(String msg);

        // public event PostMessageDel PostMessage;

        public delegate void AskRepalceDirDel(String absTarBaseDir, String tarEntryName);
        public delegate void AskRepalceFileDel(String absTarBaseDir, String tarEntryName);
        public event AskRepalceDirDel AskReplaceDir;
        public event AskRepalceFileDel AskReplaceFile;

        public delegate void TidyUpBeforeFinishDel();
        public TidyUpBeforeFinishDel TidyUpBeforeFinish = ()=>{};

        // public delegate void UpdateProgressDel(double progress);
        // public event UpdateProgressDel UpdateProgress;
        public IProgress<double> UpdateProgress;
        public IProgress<String> PostMessage;

        public void setOnFinished(OnFinishedDel onfin)
        {
            this.OnFinished = onfin;
        }
        public void resetOnFinishedCaller()
        {
            this.OnFinished = null;
        }


        public Worker(){
            this.resetCurWorker();
        }

        private bool postInitCalled = false;
        public void PostIinit(){
            postInitCalled = true;
            this.resetCurWorker();
        }

        protected void resetCurWorker(){
            this.crntly_prcsd_wrkr = this.workers.Count > 0 ? this.workers[0].FirstElementToProcess() : null;
            Console.WriteLine(string.Format("resetCurWorker: {0}", 
                        crntly_prcsd_wrkr == null ? "null" : 
                        this.crntly_prcsd_wrkr.AbsSrcPath()));
        }
        public void setWorkers(List<WorkerDirEntry> workers){
            this.workers = workers;
            this.finishedWorkers.Clear();
            this.resetCurWorker();
        }

        public void Cancel(){
            this._cancelled = true;
        }

        public Task execute(){
            return Task.Run( () =>{
                this.copyFiles();
            });
        }
        private void copyFiles(){
            if(!postInitCalled){
                this.PostIinit();
            }

            this.evalEntryCount();

            this.copyCurrentWorkerEntry();
        }

        private void _cancel(){
            this._cancelled = true;

            this.finish();
        }
        private void evalEntryCount(){
            this.PostMessage?.Report("processing...");
            this.entriesToProcess = 0;
            foreach(var worker in this.workers){
                this.entriesToProcess += worker.EntryCount;
            }
        }
        private void copyCurrentWorkerEntry(){
            var worker_entry = this.crntly_prcsd_wrkr;
            if(worker_entry == null){
                this.finish();
                return;
            }

            var absSrcPath = worker_entry.AbsSrcPath();
            this.updateCurrentlyProcessedEntry(absSrcPath);

            if( !StaticFunctions.EntryExists(absSrcPath) && worker_entry.SrcMustExist ){
                Console.WriteLine(string.Format("{0}: copyWorkerEntry: absSrcPath '{1}' does not exist!", this.Name, absSrcPath));
                this.finishCurrentWorkerEntry(false);
                return;
            }

            // absSrcPath = worker_entry.AbsSrcPath();
            var absTarBaseDir = worker_entry.AbsTarBasePath();
            var tarEntryName = worker_entry.TarEntryName;

            var absTarPath = Path.Join(absTarBaseDir, tarEntryName);

            if( StaticFunctions.EntryExists(absTarPath) && worker_entry.TarMustNotExist ){
                // falls absTarPath bereits exisitert, user fragen ob:
                //     1. absTarPath geloescht werden soll,
                //     2. falls nicht geloscht werden soll, user nach alternativem tarEntryName fragen:
                this.askUserIfTarPathShouldBeReplaced(absTarBaseDir, tarEntryName);
            }else{
                var success = this.crntly_prcsd_wrkr.ProcessFunc(absSrcPath, absTarPath);
                Console.WriteLine(string.Format("{0} - ProcessFunc - success: {1}", absSrcPath, success));
                this.finishCurrentWorkerEntry(success);
            }
        }
        private void askUserIfTarPathShouldBeReplaced(String absTarBaseDir, String tarEntryName){
            var absTarPath = Path.Join(absTarBaseDir, tarEntryName);
            if( Directory.Exists(absTarPath) ){
                this.AskReplaceDir?.Invoke(absTarBaseDir, tarEntryName);
            }else if(File.Exists(absTarPath) ){
                this.AskReplaceFile?.Invoke(absTarBaseDir, tarEntryName);
            }else{
                throw new ArgumentException(String.Format("Worer::askUserIfTarPathShouldBeReplaced - absTarPath '{0}' is neither a file nor a directory!", absTarPath));
            }
        }
        public void ReceiveValidEntryName(String entryName){
            this.receiveValidEntryName_hlpr(entryName);
        }
        private void receiveValidEntryName_hlpr(String usrSlctedEntryName){
            if( String.IsNullOrEmpty(usrSlctedEntryName) ){
                this._cancel();
                return;
            }
            if( this.crntly_prcsd_wrkr == null ){
                throw new ArgumentException("receiveValidEntryName - self.crntly_prcsd_wrkr is None!!!");
            }

            this.crntly_prcsd_wrkr.TarEntryName = usrSlctedEntryName;

            var absTarPath = this.crntly_prcsd_wrkr.AbsTarPath();
            var absSrcPath = this.crntly_prcsd_wrkr.AbsSrcPath();

            if( StaticFunctions.EntryExists(absTarPath) ){
                Console.WriteLine("ReceiveValidEntryName - self.crntly_prcsd_wrkr is None!!!");
                this.finishCurrentWorkerEntry(false);
                return;
            }else{
                var success = this.crntly_prcsd_wrkr.ProcessFunc(absSrcPath, absTarPath);
                this.finishCurrentWorkerEntry(success);
            }
        }
        private void skipCurrentWorkerEntry(){
            var worker = this.crntly_prcsd_wrkr;
            if( worker == null){
                Console.WriteLine("finishCurrentWorkerEntry - self.crntly_prcsd_wrkr is None!!!");
                this.finish();
                return;
            }
            worker.PostProcess();
            this.incrementProgress(worker.EntryCount);
            this.processNextWorkerEntry();
        }
        private void _setWorkerFinishedFlagRec(WorkerDirEntry worker){
            worker.setFinished(true);
            this.finishedWorkers.Add(worker);
            foreach(var sw in worker.sub_workerDirEntries){
                this._setWorkerFinishedFlagRec(sw);
            }
            foreach(var sw in worker.sub_workerFileEntries){
                this._setWorkerFinishedFlagRec(sw);
            }
        }
        private void finishCurrentWorkerEntry(bool success){
            var worker = this.crntly_prcsd_wrkr;
            if( worker == null){
                Console.WriteLine("finishCurrentWorkerEntry - self.crntly_prcsd_wrkr is None!!!");
                this.finish();
                return;
            }
            worker.setFinished(success);
            this.finishedWorkers.Add(worker);
            this.incrementProgress();

            if( !success && worker.AbortIfFailed ){
                this._cancel();
            }else{
                this.processNextWorkerEntry();
            }
        }
        private void processNextWorkerEntry(){
            if(this._cancelled){
                this.finish();
                return;
            }
            if( this.crntly_prcsd_wrkr != null ){
                this.crntly_prcsd_wrkr = this.crntly_prcsd_wrkr.Next();
            }

            if( this.crntly_prcsd_wrkr == null){
                foreach(var worker in this.workers){
                    if( ! this.finishedWorkers.Any(w => w == worker) ){
                        this.crntly_prcsd_wrkr = worker.FirstElementToProcess();
                        break;
                    }
                }
            }

            if( this.crntly_prcsd_wrkr != null ){
                Console.WriteLine(String.Format("\n{0}: Worker::processNextWorkerEntry - next worker found -> absSrcPath: {1}", this.Name, this.crntly_prcsd_wrkr.AbsSrcPath()));
                var msg = String.Format("copying {0}...", this.crntly_prcsd_wrkr.EntryName);
                this.PostMessage?.Report(msg);
                this.copyCurrentWorkerEntry();
            }else{
                Console.WriteLine("\nWorker::processNextWorkerEntry - no next worker found -> finishing...");
                this.finish();
            }
        }
        protected void finish(){
            var totalSuccess = !this._cancelled && this.workers.All( worker => worker.EvalSuccess() );
            this.TidyUpBeforeFinish?.Invoke();
            this.OnFinished?.Invoke(totalSuccess);
        }
        private void incrementProgress(long incrmnt = 1){
            this.entriesAlrProcessed += incrmnt;
            this.updateProgress();
        }
        private void updateProgress(){
            var prgrs = (this.entriesToProcess > 0) ? (this.entriesAlrProcessed / this.entriesToProcess) : 0.0;
            this.UpdateProgress?.Report(prgrs);
        }
        private void updateCurrentlyProcessedEntry(String absPath){
            if( !String.IsNullOrEmpty(absPath) ){
                var entryName = StaticFunctions.getEntryName(absPath);
                this.PostMessage?.Report( String.Format("Processing {0}", String.IsNullOrEmpty(entryName) ? absPath : entryName) );
            }
        }
    }

    //-------------------------------------------------------------------

    class WorkerDirEntry{
        private String entryName;
        private String baseDir;
        private String tarBaseDir;
        private String tarEntryName;
        public String EntryName {get{return entryName;}}
        public String BaseDir {get{return baseDir;}}
        public String TarBaseDir {get{return tarBaseDir;}}
        public String TarEntryName {get{return tarEntryName;} set{tarEntryName = value;}}
        private WorkerDirEntry parentWorkerDirEntry;
        public List<WorkerDirEntry> sub_workerDirEntries = new List<WorkerDirEntry>();
        public List<WorkerDirEntry> sub_workerFileEntries = new List<WorkerDirEntry>();
        private List<WorkerDirEntry> finsihedWorkers = new List<WorkerDirEntry>();
        private bool _isFile = false;
        private bool _isDir = false;
        private bool _isLink = false;
        private bool _failed = false;
        private bool _finished = false;
        public bool IsFile {get{return _isFile;}}
        public bool IsDir {get{return _isDir;}}
        public bool IsLink {get{return _isLink;}}
        public bool Failed {get{return _failed;}}
        public bool Succeeded {get{return _finished && !_failed;}}
        public bool Finished {get{return _finished;}}
        public bool AbortIfFailed {get;set;} = true;
        private bool _bottomUp = false;
        public bool BottomUp {get{return _bottomUp;} set{setBottomUp_hlpr(value);} }
        public bool SrcMustExist {get;set;} = true;
        public bool TarMustNotExist {get;set;} = true;
        public long DirCount {get{return this.evalEntryCount_hlpr().Item1;}}
        public long FileCount {get{return this.evalEntryCount_hlpr().Item2;}}
        public long EntryCount {get{
            (var dirc, var filec) = this.evalEntryCount_hlpr();
            return dirc + filec;
        }}
        public ProcessFuncDel ProcessFunc {get;set;} = (srcPath, tarPath) => true;

        public delegate bool ProcessFuncDel(string absSrcPath, string absTarPath);
        public delegate void PostProcessDel();
        public PostProcessDel PostProcess = ()=>{};

        // -----------------------------------------------
        protected void postInit(){
            ProcessFunc = (absSrcPath, absTarPath) => true;
            this.tarEntryName = this.tarEntryName != null ? this.tarEntryName :
                                                            this.entryName;
        }
        // -----------------------------------------------
        public WorkerDirEntry FirstElementToProcess(){
            Console.WriteLine(string.Format("bottmUp: {0}", this.BottomUp));
            if(this.BottomUp){
                return this.getAbsChild();
            }else{
                return this.getAbsParent();
            }
        }
        // -----------------------------------------------
        public void SetSucceeded(){
            this.setFinished(true);
        }
        public void SetFailed(){
            this.setFinished(false);
        }
        private void setBottomUp_hlpr(bool btmUp){
            this._bottomUp = btmUp;
            foreach(var sd in this.sub_workerDirEntries){
                sd.setBottomUp_hlpr(btmUp);
            }
            foreach(var sf in this.sub_workerFileEntries){
                sf.setBottomUp_hlpr(btmUp);
            }
        }
        private WorkerDirEntry getAbsParent(){
            return this.parentWorkerDirEntry != null ? this.parentWorkerDirEntry.getAbsParent() : this;
        }
        private WorkerDirEntry getAbsChild(){
            Console.WriteLine(string.Format("getAbsChild: {0} - dirs: {1} | files: {2}", 
                this.AbsSrcPath(),
                this.sub_workerDirEntries.Count,
                this.sub_workerFileEntries.Count));
            if(this.sub_workerDirEntries.Count > 0){
                return this.sub_workerDirEntries[0].getAbsChild();
            }else if(this.sub_workerFileEntries.Count > 0){
                return this.sub_workerFileEntries[0].getAbsChild();
            }else{
                return this;
            }
        }
        // -----------------------------------------------
        public void reset(){
            if(this.parentWorkerDirEntry != null){
                this.parentWorkerDirEntry.reset();
            }else{
                this.reset_hlpr();
            }
        }
        private void reset_hlpr(){
            this._finished = false;
            this._failed = false;
            this.finsihedWorkers.Clear();

            foreach(var sw in this.sub_workerDirEntries){
                sw.reset_hlpr();
            }
            foreach(var sw in this.sub_workerFileEntries){
                sw.reset_hlpr();
            }
        }
        // -----------------------------------------------
        public void setFinished(bool success){
            this._finished = true;
            this._failed = !success;
            this.tellParentThisWorkerHasFinished();
        }
        // -----------------------------------------------
        private void tellParentThisWorkerHasFinished(){
            if(this.parentWorkerDirEntry != null){
                this.parentWorkerDirEntry.finsihedWorkers.Add(this);
            }
        }
        public bool EvalSuccess(){
            return this.parentWorkerDirEntry?.EvalSuccess() ?? this.evalSuccess_hlpr();
        }
        private bool evalSuccess_hlpr(){
            return !this.Failed && this.Finished
                    && (from sd in this.sub_workerDirEntries  select sd.evalSuccess_hlpr()).All(v => v) 
                    && (from sf in this.sub_workerFileEntries select sf.evalSuccess_hlpr()).All(v => v);
        }
        // -----------------------------------------------
        private (long, long) evalEntryCount_hlpr(){
            long dirCnt = 0;
            long fileCnt = this.sub_workerFileEntries.Count;
            foreach(var sd in this.sub_workerDirEntries){
                (var dc, var fc) = sd.evalEntryCount_hlpr();
                dirCnt  += dc;
                fileCnt += fc;
            }
            dirCnt  = this.IsDir  ? dirCnt  + 1 : dirCnt;
            fileCnt = this.IsFile ? fileCnt + 1 : fileCnt;
            return (dirCnt, fileCnt);
        }
        // -----------------------------------------------
        public String AbsSrcPath(){
            var baseDir = this.AbsSrcBasePath();
            var entryName = this.entryName;
            return Path.Join(baseDir, entryName);
        }
        public String AbsSrcBasePath(){
            return !string.IsNullOrEmpty(this.baseDir) ? this.baseDir : (this.parentWorkerDirEntry?.AbsSrcPath() ?? "");
        }
        // -----------------------------------------------
        public String AbsTarPath(){
            var tarBaseDir = this.AbsTarBasePath();
            var tarEntryName = this.tarEntryName;
            return Path.Join(tarBaseDir, tarEntryName);
        }
        public String AbsTarBasePath(){
            var t = this.tarBaseDir != null ? this.tarBaseDir : (this.parentWorkerDirEntry?.AbsTarPath() ?? "");
            Console.WriteLine(string.Format("AbsSrcPath: {0}    absTarBasePath: {1}", this.AbsSrcPath(), t));
            return this.tarBaseDir != null ? this.tarBaseDir : (this.parentWorkerDirEntry?.AbsTarPath() ?? "");
        }
        // -----------------------------------------------
        public String RelTarPath(){
            var relTarBaseDir = this.RelTarBasePath();
            var tarEntryName = this.tarEntryName;
            return Path.Join(relTarBaseDir.AsSpan(), tarEntryName.AsSpan());
        }
        public String RelTarBasePath(){
            return this.tarBaseDir != null ? "" : (this.parentWorkerDirEntry?.RelTarPath() ?? "");
        }
        // -----------------------------------------------
        public bool HasNext(){
            return this.Next() != null;
        }
        public WorkerDirEntry Next(){
            Console.WriteLine(string.Format("Next: absSrcPath: {0} - finished: {1} | failed: {2} | succeeded: {3} | bottomUp: {4}", this.AbsSrcPath(), this.Finished, this.Failed, this.Succeeded, this.BottomUp));
            if( !(this.Finished || this.Failed) ){
                return this;
            }
            Console.WriteLine(string.Format("{0}: bottomUp: {1}", this.AbsSrcPath(), this.BottomUp));
            if(BottomUp){
                return this.getNext_SubEntry_BottomUp_hlpr(this.parentWorkerDirEntry);
            }else{
                foreach(var sd in this.sub_workerDirEntries){
                    if( !this.subEntryFinished(sd) ){
                        return sd;
                    }
                }
                foreach(var sf in this.sub_workerFileEntries){
                    if( !this.subEntryFinished(sf) ){
                        return sf;
                    }
                }
                return this.parentWorkerDirEntry?.Next() ?? null;
            }
        }
        private WorkerDirEntry getNext_SubEntry_BottomUp_hlpr(WorkerDirEntry worker){
            if( worker == null){
                return null;
            }
            Console.WriteLine(string.Format("WorkerDirEntry::getNext-BottomUp: {0} | subDirs: {1} | files: {2}", 
                worker.sub_workerDirEntries.Count,
                worker.sub_workerFileEntries.Count,
                worker.AbsSrcPath()));
            foreach(var sd in worker.sub_workerDirEntries){
                if( !worker.subEntryFinished(sd) ){
                    return worker.getNext_SubEntry_BottomUp_hlpr(sd);
                }
            }
            foreach(var sf in worker.sub_workerFileEntries){
                if( !worker.subEntryFinished(sf) ){
                    return worker.getNext_SubEntry_BottomUp_hlpr(sf);
                }
            }
            Console.WriteLine(string.Format(" - lastcond: finished: {0} | failed: {1} | parentnull: {2}",
                worker.Finished,
                worker.Failed,
                worker.parentWorkerDirEntry == null));
            return (worker.Finished || worker.Failed) ? worker.parentWorkerDirEntry : worker;
        }
        private bool subEntryFinished(WorkerDirEntry se){
            return this.finsihedWorkers.Any(fw => fw == se);
        }
        // -----------------------------------------------
        public void evalSubEntries(){
            var absPath = this.AbsSrcPath();
            if( Directory.Exists(absPath) ){
                foreach(var absSubDirPath in Directory.GetDirectories(absPath)){
                    var den = StaticFunctions.getEntryName(absSubDirPath);
                    var swd = createWorkerDirFromPath(baseDir: null, 
                                                      entryName: den,
                                                      parentWorkerDirEntry: this,
                                                      processFunc: this.ProcessFunc);
                    this.sub_workerDirEntries.Add(swd);
                }
                foreach(var absFilePath in Directory.GetFiles(absPath)){
                    var fen = StaticFunctions.getEntryName(absFilePath);
                    var swf = createWorkerDirFromPath(baseDir: null, 
                                                      entryName: fen,
                                                      parentWorkerDirEntry: this,
                                                      processFunc: this.ProcessFunc);
                    this.sub_workerFileEntries.Add(swf);
                }
            }
        }
        public WorkerDirEntry(String entryName,
                              String baseDir,
                              WorkerDirEntry parentWorkerDirEntry,
                              String tarBaseDir,
                              String tarEntryName){
            this.entryName = entryName;
            this.baseDir = baseDir;
            this.parentWorkerDirEntry = parentWorkerDirEntry;
            this.tarBaseDir = tarBaseDir;
            this.tarEntryName = tarEntryName;
        }
        public static WorkerDirEntry createWorkerDirFromPath(String baseDir,
                                                             String entryName = null,
                                                             WorkerDirEntry parentWorkerDirEntry = null,
                                                             String tarBaseDir=null,
                                                             String tarEntryName=null,
                                                             ProcessFuncDel processFunc=null,
                                                             bool recursive=true){
            var bd = baseDir;
            var en = entryName;
            var dirinfo = String.IsNullOrEmpty(bd) ? null : new DirectoryInfo(bd);
            baseDir   = String.IsNullOrEmpty(en) ? StaticFunctions.getBaseDirectory(bd) :  null;
            entryName = String.IsNullOrEmpty(en) ? StaticFunctions.getEntryName(bd) : en;
            tarEntryName = String.IsNullOrEmpty(tarEntryName) ? entryName : tarEntryName;

            // Console.WriteLine("-------------------------");
            // Console.WriteLine("baseDir: " + baseDir);
            // Console.WriteLine("entryName: " + entryName);
            // Console.WriteLine("tarEntryName: " + tarEntryName);
            // Console.WriteLine("parentWorkerDirEntry.absPath: " + (parentWorkerDirEntry != null ? parentWorkerDirEntry.AbsSrcPath() : "") );
            // Console.WriteLine("-------------------------");
            
            if(processFunc == null){
                processFunc = (absSrcPath, absTarPath) => true;
            }

            var we = new WorkerDirEntry(entryName: entryName,
                                        baseDir: baseDir,
                                        parentWorkerDirEntry: parentWorkerDirEntry,
                                        tarBaseDir: tarBaseDir,
                                        tarEntryName: tarEntryName);
            we.ProcessFunc = processFunc;

            var absPath = we.AbsSrcPath();

            we._isDir  = Directory.Exists(absPath);
            we._isFile = File.Exists(absPath);
            we._isLink = false;//File.

            if(recursive){
                we.evalSubEntries();
            }

            return we;
        }
        public override String ToString(){
            return String.Format("src: {0}\ttar: {1}", 
                                 this.AbsSrcPath(),
                                 this.AbsTarPath());
        }
        public void print()
        {
            print_hlpr();
        }
        private string genTabs(int cnt)
        {
            var tbs = "";
            for(int i=0; i < cnt; ++i)
            {
                tbs += "\t";
            }
            return tbs;
        }
        private void print_hlpr(int id=0)
        {
            var tabs = genTabs(id);
            Console.WriteLine(string.Format("{0}{1}", tabs, EntryName));
            foreach(var fle in this.sub_workerFileEntries)
            {
                fle.print_hlpr(id+1);
            }
            foreach(var sd in this.sub_workerDirEntries)
            {
                sd.print_hlpr(id+1);
            }
        }
    }
}
