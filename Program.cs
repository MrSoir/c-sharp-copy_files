using System;

namespace Program
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var srcPaths = new string[]{"/home/hippo/Documents/tests_src"};
            var tarPath = "/home/hippo/Documents/tests_tar";
            var moveTarPath = "/home/hippo/Documents/tests_tar/moveTar";
            
            var tarZipPath = "/home/hippo/Documents/tests_tar/tarZipFile.zip";
            var srcZipPath = "/home/hippo/Documents/ballin_test_toDelete (copy)/Ballin.zip";
            var extractionDir = "/home/hippo/Documents/tests_tar/ZipExtractionDir";
            // var cpyf = new COPY_FILES.CopyFiles(srcPaths, tarPath);
            // cpyf.execute().Wait();

            // var delf = new DELETE_FILES.DeleteFiles(new string[]{tarPath + "/tests_src"});
            // delf.execute().Wait();

            // var dupf = new DUPLICATE_FILES.DuplicateFiles(new string[]{
            //     "/home/hippo/Documents/tests_src/Effective_DevOps.pdf",
            //     "/home/hippo/Documents/tests_src/Gosh_Notes.html",
            //     "/home/hippo/Documents/tests_src/script.py",
            //     "/home/hippo/Documents/tests_src/zipped_files"
            // });
            //dupf.execute().Wait();

            // var mvf = new MOVE_FILES.MoveFiles(new string[]{
            //     "/home/hippo/Documents/tests_src/Effective_DevOps.pdf",
            //     "/home/hippo/Documents/tests_src/Gosh_Notes.html",
            //     "/home/hippo/Documents/tests_src/script.py",
            //     "/home/hippo/Documents/tests_src/zipped_files"
            // }, moveTarPath);
            //mvf.execute().Wait();

            // var zipf = new ZIP_FILES.ZipFiles(new string[]{
            //     "/home/hippo/Documents/tests_src/Effective_DevOps.pdf",
            //     "/home/hippo/Documents/tests_src/test/Effective_DevOps.pdf",
            //     "/home/hippo/Documents/tests_src/Gosh_Notes.html",
            //     "/home/hippo/Documents/tests_src/script.py",
            //     "/home/hippo/Documents/tests_src/zipped_files_small",
            //     "/home/hippo/Documents/tests_src/zipped_subpar/zipped_files_small"
            // }, tarZipPath);
            // zipf.execute().Wait();

            var uzipf = new UNZIP_FILE.UnzipFile(srcZipPath,
                                                 extractionDir);
            Console.WriteLine("\n\nexecuting...\n\n");
            uzipf.execute().Wait();
            
        }
    }
}