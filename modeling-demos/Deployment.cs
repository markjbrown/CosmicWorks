﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using cosmos_management;


namespace modeling_demos
{
    class Deployment
    {

        // private static readonly string gitdatapath = config["gitdatapath"];
        //private static string gitdatapath = "https://api.github.com/repos/MicrosoftDocs/mslearn-cosmosdb-modules-central/contents/data/fullset/";
        private static string gitdatapath = "https://api.github.com/repos/AzureCosmosDB/CosmicWorks/contents/data/";


        public static async Task LoadDatabase(CosmosClient cosmosDBClient, bool force=false, int? schemaVersion=null)
        {

            await GetFilesFromRepo("database-v1", force);
            await GetFilesFromRepo("database-v2", force);
            await GetFilesFromRepo("database-v3", force);
            await GetFilesFromRepo("database-v4", force);

            LoadContainersFromFolder(cosmosDBClient, "database-v1", "database-v1");
            LoadContainersFromFolder(cosmosDBClient, "database-v2", "database-v2");
            LoadContainersFromFolder(cosmosDBClient, "database-v3", "database-v3");
            LoadContainersFromFolder(cosmosDBClient, "database-v4", "database-v4");

        }

        

        public static async Task DeleteAllDatabases(Management management)
        {
            await management.DeleteAllCosmosDBDatabaes();
        }
        

        public static async Task CreateDatabaseAndContainers(Management management)
        {

            Console.WriteLine($"Creating database and containers for schema database-v1");
            await management.CreateOrUpdateCosmosDBDatabase("database-v1");
            await management.CreateOrUpdateCosmosDBContainer("database-v1", "customer", "/id");
            await management.CreateOrUpdateCosmosDBContainer("database-v1", "customerAddress", "/id");
            await management.CreateOrUpdateCosmosDBContainer("database-v1", "customerPassword", "/id");
            await management.CreateOrUpdateCosmosDBContainer("database-v1", "product", "/id");
            await management.CreateOrUpdateCosmosDBContainer("database-v1", "productCategory", "/id");
            await management.CreateOrUpdateCosmosDBContainer("database-v1", "productTag", "/id");
            await management.CreateOrUpdateCosmosDBContainer("database-v1", "productTags", "/id");
            await management.CreateOrUpdateCosmosDBContainer("database-v1", "salesOrder", "/id");
            await management.CreateOrUpdateCosmosDBContainer("database-v1", "salesOrderDetail", "/id");

            Console.WriteLine($"Creating database and containers for schema database-v2");
            await management.CreateOrUpdateCosmosDBDatabase("database-v2");
            await management.CreateOrUpdateCosmosDBContainer("database-v2", "customer", "/id");
            await management.CreateOrUpdateCosmosDBContainer("database-v2", "product", "/categoryId");
            await management.CreateOrUpdateCosmosDBContainer("database-v2", "productCategory", "/type");
            await management.CreateOrUpdateCosmosDBContainer("database-v2", "productTag", "/type");
            await management.CreateOrUpdateCosmosDBContainer("database-v2", "salesOrder", "/customerId");

            Console.WriteLine($"Creating database and containers for schema database-v3");
            await management.CreateOrUpdateCosmosDBDatabase("database-v3");
            await management.CreateOrUpdateCosmosDBContainer("database-v3", "customer", "/id");
            await management.CreateOrUpdateCosmosDBContainer("database-v3", "product", "/categoryId");
            await management.CreateOrUpdateCosmosDBContainer("database-v3", "productCategory", "/type");
            await management.CreateOrUpdateCosmosDBContainer("database-v3", "productTag", "/type");
            await management.CreateOrUpdateCosmosDBContainer("database-v3", "salesOrder", "/customerId");

            Console.WriteLine($"Creating database and containers for schema database-v4");
            await management.CreateOrUpdateCosmosDBDatabase("database-v4");
            await management.CreateOrUpdateCosmosDBContainer("database-v4", "customer", "/customerId");
            await management.CreateOrUpdateCosmosDBContainer("database-v4", "product", "/categoryId");
            await management.CreateOrUpdateCosmosDBContainer("database-v4", "productMeta", "/type");
            await management.CreateOrUpdateCosmosDBContainer("database-v4", "salesByCategory", "/categoryId");
            
        }

        private static async Task GetFilesFromRepo(string databaseName, bool force = false)
        {
            string folder = "data" + Path.DirectorySeparatorChar + databaseName;
            string url = gitdatapath + databaseName;
            Console.WriteLine("Geting file info from repo");
            HttpClient httpClient = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "cosmicworks-samples-client");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            HttpResponseMessage response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Error reading sample data from GitHub");
                Console.WriteLine($" - {url}");
                return;
            }

            String directoryJson = await response.Content.ReadAsStringAsync(); ;

            GitFileInfo[] dirContents = JsonConvert.DeserializeObject<GitFileInfo[]>(directoryJson);
            var downloadTasks = new List<Task>();

            foreach (GitFileInfo file in dirContents)
            {
                if (file.type == "file")
                {
                    Console.WriteLine($"File {file.name} {file.size}");
                    var filePath = folder + Path.DirectorySeparatorChar + file.name;


                    Boolean downloadFile = true;
                    if (File.Exists(filePath))
                    {
                        if (new System.IO.FileInfo(filePath).Length == file.size)
                        {
                            Console.WriteLine("    File exists and matches size");
                            downloadFile = false;
                            if (force == true) downloadFile = true;
                        }
                    }

                    if (downloadFile)
                    {
                        Console.WriteLine($"   Download path {file.download_url}");
                        Console.WriteLine("    Started download...");
                        downloadTasks.Add(HttpGetFile(file.download_url, filePath));
                    }
                }
            }

            Task downloadTask = Task.WhenAll(downloadTasks);
            try
            {
                downloadTask.Wait();
            }
            catch (AggregateException ex)
            {

            }

            if (downloadTask.Status == TaskStatus.Faulted)
            {
                Console.WriteLine("Files failed to download");
                foreach (var task in downloadTasks)
                {
                    Console.WriteLine("Task {0}: {1}", task.Id, task.Status);
                    Console.WriteLine(task.Exception.ToString());
                }
            }
            if (downloadTask.Status == TaskStatus.RanToCompletion) Console.WriteLine("Files download sucessfully");
        }

        private static void LoadContainersFromFolder(CosmosClient client, string SourceDatabaseName, string TargetDatabaseName, bool useBulk=true)
        {
            if(useBulk)
            {
                client.ClientOptions.AllowBulkExecution = true;
            }
            string folder = "data" + Path.DirectorySeparatorChar + SourceDatabaseName;
            Database database = client.GetDatabase(TargetDatabaseName);
            Console.WriteLine("Preparing to load containers");
            string[] fileEntries = Directory.GetFiles(folder);
            List<Task> concurrentLoads = new List<Task>();
            foreach (string fileName in fileEntries)
            {
                var containerName = fileName.Split(Path.DirectorySeparatorChar)[2];
                Console.WriteLine($"    Container {containerName} from {fileName}");
                try
                {
                    Container container = database.GetContainer(containerName);
                    concurrentLoads.Add(LoadContainerFromFile(container, fileName));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error connecting to container {containerName} ");
                    Console.WriteLine(ex.ToString());
                }
            }
            Task concurrentLoad = Task.WhenAll(concurrentLoads);
            try
            {
                concurrentLoad.Wait();
            }
            catch (AggregateException ex)
            {

            }

            if (concurrentLoad.Status == TaskStatus.Faulted)
            {
                Console.WriteLine("Sample data load failed");
            }

            foreach (var task in concurrentLoads)
            {
                Console.WriteLine("Task {0}: {1}", task.Id, task.Status);
                if (task.Status == TaskStatus.Faulted)
                {
                    Console.WriteLine($"Task {task.Id} {task.Exception}");

                }
            }

        }

        private static async Task LoadContainerFromFile(Container container, string file, Boolean noBulk = false)
        {
            using (StreamReader streamReader = new StreamReader(file))
            {

                int maxConcurrentTasks = 200;
                bool usebulk = !noBulk;

                string recordsJson = streamReader.ReadToEnd();
                dynamic recordsArray = JsonConvert.DeserializeObject(recordsJson);

                int batches = 0;
                int batchCounter = 0;
                int docCounter = 0;
                List<Task> concurrentTasks = new List<Task>(maxConcurrentTasks);
                int totalDocs = recordsArray.Count;
                foreach (var record in recordsArray)
                {
                    if (usebulk)
                    {
                        concurrentTasks.Add(container.CreateItemAsync(record));
                    }
                    else
                    {
                        container.CreateItemAsync(record);
                    }
                    batchCounter++;
                    if (batchCounter >= maxConcurrentTasks)
                    {
                        docCounter = docCounter + batchCounter;
                        batchCounter = 0;
                        await Task.WhenAll(concurrentTasks);
                        Console.WriteLine($"    loading {file} - batch:{batches} - documents:{docCounter} of {totalDocs}");

                        concurrentTasks.Clear();
                        batches++;
                    }

                }
                Console.WriteLine($"    loaded  {file} - batch:{batches} - documents:{docCounter} of {totalDocs}");
                await Task.WhenAll(concurrentTasks);
            }
        }

        private static async Task HttpGetFile(string url, string filename)
        {
            using (HttpClient client = new HttpClient())
            {
                using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
                {
                    using (Stream streamToWriteTo = File.Open(filename, FileMode.Create))
                    {
                        await streamToReadFrom.CopyToAsync(streamToWriteTo);
                    }
                }
            }
        }

        class GitFileInfo
        {
            public String name="";
            public String type="";
            public long size=0;
            public String download_url="";
        }

        class Secrets
        {
            public string uri="";
            public string key="";
        };

        public class SchemaDetails
        {
            public string ContainerName;
            public string Pk;
        };

    }
}
