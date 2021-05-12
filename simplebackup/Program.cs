using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;


namespace simplebackup
{
    public class SyncResultUpdateViewModel
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string Status { get; set; }
        public object DownloadContentLen { get; set; }
    }
    public class FtpconnectionObject
    {
        private string Server { get; set; }
        private int ftpPort { get; set; }
        private string destinationPath { get; set; }
        private string ftpUsername { get; set; }
        private string ftpPassword { get; set; }
        private string sourcePath { get; set; }
        private int maxThread { get; set; }
        private string FullUrl { get { return String.Format("ftp://{0}:{1}/{2}", Server, ftpPort, sourcePath); } }

     

        public SemaphoreSlim semaphore { get; set; }
        private int defaultThread = 10;
        List<Task> toDownloads = new List<Task>();
        List<SyncResultUpdateViewModel> currentQueue = new List<SyncResultUpdateViewModel>();
        List<SyncResultUpdateViewModel> forReQueue = new List<SyncResultUpdateViewModel>();
  
        public FtpconnectionObject(string _server, int _ftpPort, string _destinationPath, string _sourcePath, string _ftpUsername, string _ftpPassword, string _maxThread)
        {
            Server = _server;
            ftpPort = _ftpPort;
            destinationPath = _destinationPath;
            ftpUsername = _ftpUsername;
            ftpPassword = _ftpPassword;
            sourcePath = _sourcePath;
            maxThread = String.IsNullOrEmpty(_maxThread) || String.IsNullOrWhiteSpace(_maxThread) ? defaultThread : int.Parse(_maxThread);
            semaphore = new SemaphoreSlim(maxThread);
        }





        private void PrintStatus()
        {
            string[] forMonitorStatus = new string[] { "Started" };
            while (true)
            {
                Console.Clear();

                foreach (var item in currentQueue.Where(z => forMonitorStatus.Contains(z.Status)))
                {
                    Console.WriteLine($"Id: {item.Id} | filename: { item.FileName } | Status: {item.Status}");
                }

                Thread.Sleep(1000);
            }

        }
        public void SyncFiles()
        {

             
                
              
                string errorMessage = validateConnectionParam();
                if (String.IsNullOrEmpty(errorMessage) && String.IsNullOrWhiteSpace(errorMessage))
                {
                    Console.WriteLine("Getting List files for download");
                    List<string> forDowload = GetFilesForDownload();
                    Console.WriteLine("Done Getting List files for download");
                    foreach (var item in forDowload)
                    {
                        currentQueue.Add(new SyncResultUpdateViewModel() { FileName = item, Status = "Queued" });
                        toDownloads.Add(DownloadFileFromFtpAsync(item));
                    }
                    Console.WriteLine("arrange List files for download done");
                    Task.Run(() => PrintStatus());
                    Task.WaitAll(toDownloads.ToArray());
                }
                else
                {
                    throw new Exception(errorMessage);
                }
           
        }
        private async Task DownloadFileFromFtpAsync(string Filename)
        {

            FtpWebRequest downloadRequest = (FtpWebRequest)WebRequest.Create(FullUrl + "/" + Filename);
            downloadRequest.Method = WebRequestMethods.Ftp.DownloadFile;
            downloadRequest.Credentials = new NetworkCredential(ftpUsername, ftpPassword);
            downloadRequest.KeepAlive = false;


            await semaphore.WaitAsync();
            _ = currentQueue.Where(z => z.FileName == Filename).FirstOrDefault().Id = Thread.CurrentThread.ManagedThreadId;
            _ = currentQueue.Where(z => z.FileName == Filename).FirstOrDefault().Status = "Started";
            try
            {
                Console.WriteLine($"Qeued : { Filename }");
                var downloadResponse = downloadRequest.GetResponseAsync();


                _ = Task.Run(async () =>
                {

                    while (true)
                    {

                        try
                        {
                            if (downloadResponse.IsCompleted)
                            {
                                using (Stream sw = downloadResponse.Result.GetResponseStream())
                                {
                                    var fileDestinationPath = Path.Combine(destinationPath, Filename);
                                    using (Stream file = File.Create(fileDestinationPath))
                                    {

                                        await sw.CopyToAsync(file);

                                    }
                                }
                                _ = currentQueue.Where(z => z.FileName == Filename).FirstOrDefault().Status = "done";
                                semaphore.Release();
                                break;
                            }

                            if (downloadResponse.IsFaulted)
                            {
                                throw new Exception(downloadResponse.IsFaulted.ToString());
                            }
                        }
                        catch (Exception e)
                        {
                            _ = currentQueue.Where(z => z.FileName == Filename).FirstOrDefault().Status = $"Error: {e.ToString()}";
                            semaphore.Release();
                            break;
                        }
                    }
                });

            }
            catch
            {
                _ = currentQueue.Where(z => z.FileName == Filename).FirstOrDefault().Status = "Error";
                semaphore.Release();
                throw new Exception("Downloading error");
            }
        }
        private string validateConnectionParam()
        {
            if (String.IsNullOrEmpty(Server) || String.IsNullOrWhiteSpace(Server))
            {
                return "Server address is Required";
            }
            if (String.IsNullOrEmpty(ftpUsername) || String.IsNullOrWhiteSpace(ftpUsername))
            {
                return "FTP username is Required";
            }
            if (String.IsNullOrEmpty(ftpPassword) || String.IsNullOrWhiteSpace(ftpPassword))
            {
                return "Server user Password is Required";
            }
            return String.Empty;
        }
        private string[] GetFtpFilesList()
        {


            FtpWebRequest listDownrequest = (FtpWebRequest)WebRequest.Create(FullUrl);
            listDownrequest.Method = WebRequestMethods.Ftp.ListDirectory;
            listDownrequest.KeepAlive = false;
            listDownrequest.UsePassive = true;

            listDownrequest.Credentials = new NetworkCredential(ftpUsername, ftpPassword);
            try
            {
                using (FtpWebResponse ListDownRequestResponse = (FtpWebResponse)listDownrequest.GetResponse())
                {
                    using (StreamReader ListReader = new StreamReader(ListDownRequestResponse.GetResponseStream()))
                    {
                        string responseString = ListReader.ReadToEnd();
                        string[] results = responseString.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        return results;
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Getting FTP files error { e.Message.ToString() }");
            }
        }
           
        private string[] getLocalFolderFileList()
        {
            DirectoryInfo directory = new DirectoryInfo(destinationPath);
            FileInfo[] files = directory.GetFiles();
            return files.Select(x => x.Name).ToArray();
        }
        private List<string> GetFilesForDownload()
        {
            List<string> focBackupFiles = new List<string>();

           
                var serverFiles = GetFtpFilesList();
                var localFiles = getLocalFolderFileList();



                focBackupFiles = serverFiles?.Except(localFiles?.Select(x => x)).ToList();
         

            return focBackupFiles;
        }
    }




    class Program
    {

        static int retryAvailability = 0;
        static void Main(string[] args)
        {
            //string[] CommandsParameter = new string[] { "-server", "-sourcePath", "-destinationPath", "-sourceUn", "-sourcePass", "-sourcePort" };

            Dictionary<string, string> commandDictionary = new Dictionary<string, string>()
            {
                {"-server", "" },
                {"-sourcePath", "" },
                {"-sourcePort", "" },
                {"-destinationPath", "" },
                {"-sourceUn", "" },
                {"-sourcePass", "" },
                {"-maxThread", "" }
            };

            if (args.Length > 0)
            {
                //gather all parameters
                for (int i = 0; i < args.Length; i++)
                {
                    var currentVal = args[i];
                    if (commandDictionary.ContainsKey(currentVal))
                    {
                        if (commandDictionary.ContainsKey(currentVal))
                        {
                            int nextIndex = i + 1;
                            commandDictionary[currentVal] = args[nextIndex];
                        }
                    }
                }

                string serverAddress;
                _ = commandDictionary.TryGetValue("-server", out serverAddress);

                string sourcePath;
                _ = commandDictionary.TryGetValue("-sourcePath", out sourcePath);

                string sourcePort;
                _ = commandDictionary.TryGetValue("-sourcePort", out sourcePort);

                string destinationPath;
                _ = commandDictionary.TryGetValue("-destinationPath", out destinationPath);

                string sourceUn;
                _ = commandDictionary.TryGetValue("-sourceUn", out sourceUn);
                string sourcePass;
                _ = commandDictionary.TryGetValue("-sourcePass", out sourcePass);

                string maxThread;
                _ = commandDictionary.TryGetValue("-maxThread", out maxThread);


                FtpconnectionObject obj = new FtpconnectionObject(serverAddress, int.Parse(sourcePort), destinationPath, sourcePath, sourceUn, sourcePass, maxThread);
              try
                {
                    obj.SyncFiles();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error in connecting:" + e.Message.ToLower().ToString());
                    System.Diagnostics.Debug.WriteLine("Error in connecting:" + e.Message.ToLower().ToString());
                    if (retryAvailability < 100)
                    {
                        Console.WriteLine("retrying connection.....");
                        Console.WriteLine("Error in connecting:" + e.Message.ToLower().ToString());
                        Thread.Sleep(10000);
                        obj.SyncFiles();
                    }

                   
                }
                 
               
             

            }
            else
            {
                Console.WriteLine("Cannot accept null arguments");
            }

        }




    }
}

