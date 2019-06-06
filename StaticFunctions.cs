using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.IO.Compression;

namespace STATIC_FUNCTIONS
{
    public class StaticFunctions
    {
        public enum USER_ANSWER{
            OK, 
            NO, 
            CANCEL
        }
        public enum ENTRY_TYPE{
            FILE,
            DIRECTORY
        }

        public static ZipArchiveEntry[] getEntriesFromArchive(string zipArchivePath)
        {
            try{
                using(var zipFileStream = new FileStream(zipArchivePath, FileMode.Open))
                {
                    using(var zipArchive = new ZipArchive(zipFileStream, ZipArchiveMode.Read))
                    {
                        return zipArchive.Entries.ToArray();
                    }
                }
            }catch(Exception e){
                Console.WriteLine(e);
                return null;
            }
        }

        public static string getEntryName(string absPath){
            return PATH_FUNCTIONS.PathFunctions.getEntryName(absPath);
        }
        public static long FileCount(List<string> absPaths)
        {
            return EntryCount_hlpr(absPaths).Item2;
        }
        public static long DirCount(List<string> absPaths)
        {
            return EntryCount_hlpr(absPaths).Item1;
        }
        public static (long, long) EntryCount_hlpr(List<string> absPaths)
        {
            var fileCount = 0;
            var dirCount = 0;
            foreach(var absPath in absPaths){
                if(Directory.Exists(absPath)){
                    dirCount += 1;
                    dirCount += Directory.GetDirectories(absPath, "", SearchOption.AllDirectories).Length;
                    fileCount += Directory.GetFiles(absPath, "", SearchOption.AllDirectories).Length;
                }else if (File.Exists(absPath)){
                    fileCount += 1;
                }
            }
            return (dirCount, fileCount);
        }
        public static bool generateDirIfNotAlrExistent(string baseDirPath, string absTarDirPath){
            if( !Directory.Exists(baseDirPath) ){
                return false;
            }
            var relPath = Path.GetRelativePath(baseDirPath, absTarDirPath);
            var relPathDirs = relPath.Split(Path.DirectorySeparatorChar);

            var curTarDir = baseDirPath;
            foreach(var dir in relPathDirs){
                curTarDir = Path.Join(curTarDir, dir);
                if( !Directory.Exists(curTarDir) ){
                    var success = createDirectory(curTarDir);
                    if(!success){
                        return false;
                    }
                }
            }
            return true;
        }
        public static bool createDirectory(string absDirPath)
        {
            try{
                if(Directory.Exists(absDirPath)){
                    return false;
                }
                Directory.CreateDirectory(absDirPath);
                return Directory.Exists(absDirPath);
            }catch (Exception){
                return false;
            }
        }
        public static bool createDirectoryIfNotExistent(string absPath)
        {
           return createDirectoryIfNotExistent_hlpr(absPath);
        }
        private static bool createDirectoryIfNotExistent_hlpr(string absPath)
        {
            if( string.IsNullOrEmpty(absPath) )
            {
                return false;
            }
            if( Directory.Exists(absPath) )
            {
                return true;
            }else{
                var baseDir =  getBaseDirectory(absPath);

                // to avoid inivinte loop (obviously something went wrong at this stage):
                if(baseDir == absPath)
                {
                    return false;
                }

                // make sure parent directory exists - if not, create it:
                bool parentDirExists = createDirectoryIfNotExistent_hlpr(baseDir);
                if(parentDirExists)
                {
                    return createDirectory(absPath);
                }else{
                    return false;
                }
            }
        }
        public static (string, string[], string[]) getEntriesInDirectory(string absDirPath){
            var files = (from fle in Directory.GetDirectories(absDirPath) where string.IsNullOrEmpty(fle) select Path.GetFileName(fle)).ToArray();
            var dirs = (from sd in Directory.GetDirectories(absDirPath) select Path.GetDirectoryName(sd)).ToArray();
            return (absDirPath, files, dirs);
        }
        public static bool copyFile(string absSrcPath, string absTarPath){
            if( String.IsNullOrEmpty(absSrcPath) ||
                String.IsNullOrEmpty(absTarPath) ||
                File.Exists(absTarPath)){
                return false;
            }
            try{
                File.Copy(absSrcPath, absTarPath, false);
                return File.Exists(absTarPath);
            }catch(Exception){
                return false;
            }
        }
        public static bool EntryExists(string absPath){
            return Directory.Exists(absPath) || File.Exists(absPath);
        }
        public static bool copyEntry(string absSrcPath, string absTarPath){
            if(String.IsNullOrEmpty(absSrcPath) ||
               String.IsNullOrEmpty(absTarPath) ||
               EntryExists(absTarPath)){
                return false;
            }
            if(Directory.Exists(absSrcPath)){
                return createDirectory(absTarPath);
            }else if(File.Exists(absSrcPath)){
                return copyFile(absSrcPath, absTarPath);
            }
            return false;
        }
        public static bool moveEntry(string absSrcPath, string absTarPath){
            try{
                if(Directory.Exists(absSrcPath)){
                    Directory.Move(absSrcPath, absTarPath);
                }else if(File.Exists(absSrcPath)){
                    File.Move(absSrcPath, absTarPath);
                }
                return !EntryExists(absSrcPath) && EntryExists(absTarPath);
            }catch(Exception){
                return false;
            }
        }
        public static string getBaseDirectory(string absSrcPath){
            return Path.GetDirectoryName(absSrcPath);
        }
        public static bool renameEntry(string absSrcPath, string newEntryName){
            if( !EntryExists(absSrcPath)){
                return false;
            }
            try{
                var baseDir = Path.GetDirectoryName(absSrcPath);
                var absTarPath = Path.Join(baseDir, newEntryName);

                if( Directory.Exists(absSrcPath)){
                    Directory.Move(absSrcPath, absTarPath);
                }else if(File.Exists(absSrcPath)){
                    File.Move(absSrcPath, absTarPath);
                }else{
                    return false;
                }
                return !EntryExists(absSrcPath) && EntryExists(absTarPath);
            }catch(Exception){
                return false;
            }
        }

        // --------------------------------------------------

        public static (USER_ANSWER, bool) copyFileAndAksUserIfAlreadyExists(string absSrcFilePath, 
                                                      string baseDirTargetPath, 
                                                      string fileName){
            var vn = evalValidFileName(baseDirTargetPath, fileName);
            if(vn.Item1 == USER_ANSWER.CANCEL){
                return (USER_ANSWER.CANCEL, false);
            }else if(String.IsNullOrEmpty(vn.Item2)){
                var validFileName = vn.Item2;
                var absTarFilePath = Path.Join(baseDirTargetPath, validFileName);
                if(EntryExists(absTarFilePath)){
                    return (USER_ANSWER.OK, false);
                }
                return (USER_ANSWER.OK, copyFile(absSrcFilePath, absTarFilePath));
            }
            return (USER_ANSWER.NO, false);
        }
        public static USER_ANSWER showYesNoCancelDialog(string message,
                                                 string caption){
            return USER_ANSWER.OK;
        }

        public static bool deleteDir(string absDirPath, bool recursive=true){
            if( string.IsNullOrEmpty(absDirPath) || 
                absDirPath == "" + Path.DirectorySeparatorChar ||
                !Directory.Exists(absDirPath)){
                return false;
            }
            try{
                Directory.Delete(absDirPath, recursive);
            }catch(Exception){
                return false;
            }
            return !Directory.Exists(absDirPath);
        }
        public static bool deleteFile(string absFilePath){
            if(!File.Exists(absFilePath)){
                return false;
            }
            try{
                File.Delete(absFilePath);
            }catch(Exception){
                return false;
            }
            return !File.Exists(absFilePath);
        }
        public static bool deleteEntry(string absEntryPath){
            if(string.IsNullOrEmpty(absEntryPath) || !EntryExists(absEntryPath)){
                return false;
            }
            if(Directory.Exists(absEntryPath)){
                return deleteDir(absEntryPath);
            }else if(File.Exists(absEntryPath)){
                return deleteFile(absEntryPath);
            }
            return false;
        }
        // ------------------------------------
        public static (USER_ANSWER, string) askForValidDirectoryPath(string absDirPath)
        {
            var baseDir = getBaseDirectory(absDirPath);
            var dirName = getEntryName(absDirPath);

            var answr = askForValidDirectoryName(baseDir, dirName);

            if(answr.Item1 == USER_ANSWER.OK){
                return (answr.Item1, Path.Join(baseDir, answr.Item2));
            }
            return answr;
        }

        public static (USER_ANSWER, string) askForValidFilePath(string absDirPath)
        {
            var baseDir = getBaseDirectory(absDirPath);
            var fileName = getEntryName(absDirPath);

            var answr = askForValidFileName(baseDir, fileName);

            if(answr.Item1 == USER_ANSWER.OK){
                return (answr.Item1, Path.Join(baseDir, answr.Item2));
            }
            return answr;
        }
        public static (USER_ANSWER, string) askForValidFileName(string baseDir, string initFileName){
            return askForValidEntryName(baseDir, initFileName, ENTRY_TYPE.FILE);
        }
        public static (USER_ANSWER, string) askForValidDirectoryName(string baseDir, string initDirName){
            return askForValidEntryName(baseDir, initDirName, ENTRY_TYPE.DIRECTORY);
        }
        public static (USER_ANSWER, string) askForValidEntryName(string baseDir, string initEntryName, ENTRY_TYPE entryType=ENTRY_TYPE.FILE){
            return (USER_ANSWER.OK, genRandomString(initEntryName));
        }
        public static string evalValidDirectoryName(string absTarDirPath){
            var dirName = getEntryName(absTarDirPath);
            var tarBaseDir = getBaseDirectory(absTarDirPath);
            if( !string.IsNullOrEmpty(dirName) &&
                !string.IsNullOrEmpty(tarBaseDir) &&
                Path.Join(tarBaseDir, dirName) == absTarDirPath){
                return evalValidSubDirName(tarBaseDir, dirName);
            }
            Console.WriteLine("return false condition!!!");
            return null;
        }
        public static string evalValidSubDirName(string tarDir, string subDirName){
            return tarDir + genRandomString(subDirName);
        }
        public static string genRandomString(string baseStr){
            var ts = baseStr;
            var rnd = new Random();
            for(var i=0; i < 10; ++i){
                ts += rnd.Next(100);
            }
            return ts;
        }
        public static USER_ANSWER askUserIfHeWantsToReplaceDir(string tarBaseDir, string tarDirName){
            return USER_ANSWER.OK;
        }
        public static (USER_ANSWER, string) evalValidFileName(string tarDirName, string initFileName){
            return (USER_ANSWER.OK, genRandomString(initFileName));
        }
    }
}