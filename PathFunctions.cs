using System;
using System.IO;
using System.Collections.Generic;
using STATIC_FUNCTIONS;
using System.Linq;

namespace PATH_FUNCTIONS{
    public static class PathFunctions{


        public static string appendDirSeparatorToPath(string absPath)
        {
            return appendDirSeparatorToPath_hlpr(absPath, Path.DirectorySeparatorChar);
        }
        public static string appenUnixDirSeparatorToPath(string absPath)
        {
            return appendDirSeparatorToPath_hlpr(absPath, '/');
        }
        public static string appendDirSeparatorToPath_hlpr(string absPath, char dirPathSep)
        {
            if(string.IsNullOrEmpty(absPath))
            {
                return absPath;
            }
            
            if(absPath.Length > 0 && absPath[absPath.Length-1] == dirPathSep){
                return absPath;
            }else{
                return absPath + dirPathSep;
            }
        }

        public static string joinPathFragments(string[] frgmnts){
            if(frgmnts.Length == 0){
                return "";
            }
            return frgmnts.Skip(1).Aggregate(frgmnts[0], (f0, f1)=>{return Path.Join(f0, f1);});
        }
        public static string generateUniquePathJoined(string absPath)
        {
            (string baseDir, string tarEntryName) = generateUniquePath(absPath);
            return Path.Join(baseDir, tarEntryName);
        }
        public static (string, string) generateUniquePath(string absPath){
            (string baseDir, string entryNameWithoutExt, string ext) = generateUniqueEntry_hlpr(absPath);
            string tarEntryName =  entryNameWithoutExt + ext;
            return (baseDir, tarEntryName);
        }
        public static string generateUniqueEntryName(string absPath){
            (string baseDir, string entryNameWithoutExt, string ext) = generateUniqueEntry_hlpr(absPath);
            return entryNameWithoutExt + ext;
        }
        private static (string, string, string) generateUniqueEntry_hlpr(string absPath){
            absPath = removePotDirSepAtEnd(absPath);

            if(string.IsNullOrEmpty(absPath)){
                throw new ArgumentException(string.Format("generateUniqueEntry_hlpr: absPath is null or empty!!!"));
            }

            var baseDir = STATIC_FUNCTIONS.StaticFunctions.getBaseDirectory(absPath);
            var entryNameWithoutExt = getFileNameWithoutExtension(absPath);
            var ext = getExtension(absPath);

            var cntr = 2;
            var mninpEntryName = entryNameWithoutExt;
            var absTarPath = Path.Join(baseDir, mninpEntryName + ext);
            Console.WriteLine("absTarPath: " + absTarPath);
            while(Directory.Exists(absTarPath) || File.Exists(absTarPath)){
                mninpEntryName = entryNameWithoutExt + "_(" + cntr++ + ")";
                absTarPath = Path.Join(baseDir, mninpEntryName + ext);
            }
            return (baseDir, mninpEntryName, ext);
        }

        // removePotUnixDirSepAtEnd needed for MS-Zip-operations:
        public static string removePotUnixDirSepAtEnd(string absPath)
        {
            return removePotDirSepAtEnd_hlpr(absPath, '/');

        }
        public static string removePotDirSepAtEnd(string absPath){
            return removePotDirSepAtEnd_hlpr(absPath, Path.DirectorySeparatorChar);
        }
        private static string removePotDirSepAtEnd_hlpr(string absPath, char sep)
        {
            if(string.IsNullOrEmpty(absPath)){
                return "";
            }
            if(absPath[absPath.Length-1] == sep){
                absPath = absPath.Substring(0, absPath.Length-1);
            }
            return absPath;
        }
        public static string getFileNameWithoutExtension(string absPath){
            absPath = removePotDirSepAtEnd(absPath);
            if(string.IsNullOrEmpty(absPath)){
                return "";
            }
            var fileName = getEntryName(absPath);
            var ext = getExtension(absPath);
            var fileNameWithoutExt = fileName.Substring(0, fileName.Length - ext.Length);
            return fileNameWithoutExt;
        }
        public static string getFilePathWithoutExtension(string absPath){
            absPath = removePotDirSepAtEnd(absPath);
            if(string.IsNullOrEmpty(absPath)){
                return "";
            }
            var ext = getExtension(absPath);
            return absPath.Substring(0, absPath.Length - ext.Length);
        }
        public static string getEntryName(string absPath){
            absPath = removePotDirSepAtEnd(absPath);
            if(string.IsNullOrEmpty(absPath)){
                return "";
            }
            if(absPath[absPath.Length-1] == Path.DirectorySeparatorChar){
                absPath = absPath.Substring(0,absPath.Length-1);
            }
            if(string.IsNullOrEmpty(absPath)){
                return "";
            }

            var lastSepId = absPath.LastIndexOf(Path.DirectorySeparatorChar);
            if(lastSepId == -1){
                return absPath;
            }else if(lastSepId < absPath.Length-1){
                return absPath.Substring(lastSepId+1);
            }
            return "";
        }
        public static string getExtension(string absPath){
            if(string.IsNullOrEmpty(absPath)){
                return "";
            }
            absPath = removePotDirSepAtEnd(absPath);
            try{
                var ext = Path.GetExtension(absPath);
                return ext;
            }catch(Exception){
                return "";
            }
        }
    }
}